using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;
using Services.Imaging;
using Utilities.Flare;

public interface IComixSource : IMangaSource, IFlareImageSource, IPostProcessingSource
{

}

internal class ComixSource(
	IFlareSolverService _flare,
	ILogger<ComixSource> _logger) : IComixSource
{
#if DEBUG
	private static JsonSerializerOptions _debugOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		AllowTrailingCommas = true,
	};
#endif

	private static RateLimiter? _limiter;

	public string HomeUrl => "https://comix.to";

	public string Provider => "comix-to";

	public string? Referer => HomeUrl;

	public string Name => "Comix.to";

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	public bool UseFlareImages => true;

	public async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{HomeUrl}/title/{mangaId}-mangatitle/{chapterId}-chapter-1";
		var instance = new FlareSolverInstance(_flare, _logger)
		{
			MaxRequestsBeforePauseMin = 5,
			MaxRequestsBeforePauseMax = 15,
			ResponseWait = TimeSpan.FromSeconds(2),
			DisableMedia = false,
		};

		var doc = await instance.GetHtml(url, token);
		if (doc is null)
		{
			_logger.LogWarning("Could not find page for {Url}", url);
			return [];
		}

		var pages = ParseChapterPages(doc, doc.FlareSolution.Url);
		await DebugLog($"{mangaId}-{chapterId}", 0, doc, pages, token);
		return pages;
	}

	public static async Task DebugLog<T>(string id, int page, FlareHtmlDocument doc, T value, CancellationToken token)
	{
#if DEBUG
		const string DIR = "debug";
		if (!Directory.Exists(DIR))
			Directory.CreateDirectory(DIR);

		await File.WriteAllTextAsync($"{DIR}/debug-comix-{id}-{page}.html", doc.FlareSolution.Response, token);
		var result = JsonSerializer.Serialize(doc.FlareSolution, _debugOptions);
		await File.WriteAllTextAsync($"{DIR}/debug-comix-{id}-{page}.json", result, token);
		result = JsonSerializer.Serialize(value, _debugOptions);
		await File.WriteAllTextAsync($"{DIR}/debug-comix-{id}-{page}-data.json", result, token);
#endif
	}

	public async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		await using var session = await _flare.CreateSession(null, token);
		var instance = new FlareSolverInstance(session, _logger)
		{
			MaxRequestsBeforePauseMin = 5,
			MaxRequestsBeforePauseMax = 15,
			ResponseWait = TimeSpan.FromSeconds(2),
			DisableMedia = true,
		};

		var baseUrl = $"{HomeUrl}/title/{id}";
		var doc = await instance.GetHtml(baseUrl, token);
		if (doc is null)
		{
			_logger.LogWarning("Failed to retrieve manga page for id: {MangaId}", id);
			return null;
		}

		var manga = ParseManga(doc, id, baseUrl);
		var pagination = ParseChapterPagination(doc);

		if (manga is null || pagination is null)
		{
			_logger.LogWarning("Failed to parse manga page for id: {MangaId}", id);
			return null;
		}

		await DebugLog(id, 1, doc, manga, token);

		for(var page = 2; page <= pagination.TotalPages; page++)
		{
			var pageUrl = $"{baseUrl}?page={page}";
			var next = await instance.GetHtml(pageUrl, token);
			if (next is null)
			{
				_logger.LogWarning("Failed to retrieve chapter page {Page} for manga id: {MangaId}", page, id);
				break;
			}

			var chapters = ParseChapters(next.DocumentNode);
			await DebugLog(id, page, next, chapters, token);
			if (chapters is null || chapters.Count == 0)
				break;

			manga.Chapters.AddRange(chapters);
		}

		manga.Chapters = [..manga.Chapters
			.DistinctBy(t => t.Number)
			.OrderBy(t => t.Number)];
		return manga;
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		string URL = $"{HomeUrl}/title/";
		if (!url.StartsWith(URL, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var parts = url[URL.Length..].Split("-", StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return (false, null);

		return (true, parts.First());
	}

	public RateLimiter GetRateLimiter(string _) => _limiter ??= PolyfillExtensions.DefaultRateLimiter();

	public async Task<Image?> PostProcessing(DownloadResult result, Image image, CancellationToken token)
	{
		const string SCRAMBLE_SEED_HEADER = "X-Scramble-Seed";
		const string SCRAMBLE_GRID_HEADER = "X-Scramble-Grid";
		if (result?.Response is null) return null;

		var headers = result.Response.Headers;
		var seed = headers.TryGetValues(SCRAMBLE_SEED_HEADER, out var seedValues) ? seedValues.FirstOrDefault() : null;
		var grid = headers.TryGetValues(SCRAMBLE_GRID_HEADER, out var gridValues) ? gridValues.FirstOrDefault() : null;
		if (string.IsNullOrWhiteSpace(seed) || string.IsNullOrWhiteSpace(grid))
			return null;

		return ImageUnscrambler.Unscramble((Image<Rgba32>) image, seed, grid);
	}

	#region Manga Page Parsing
	private ImportManga ParseManga(HtmlDocument doc, string requestedId, string baseUrl)
	{
		var root = doc.DocumentNode;
		var initialData = GetInitialData(root);
		var detail = GetQuery(initialData, "manga", "detail", requestedId);

		var title = Text(detail?["title"]) ??
			Clean(root.SelectSingleNode("//h1[contains(@class,'mpage__title')]")?.InnerText) ??
			string.Empty;

		var sourceId = Text(detail?["hid"]) ??
			Text(initialData?["manga"]?["hid"]) ??
			requestedId;

		var homePage = Text(detail?["url"]) ?? baseUrl;
		if (!homePage.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
			homePage = AbsoluteUrl(homePage, HomeUrl) ?? baseUrl;
		var cover = Text(detail?["poster"]?["large"]) ??
			Text(detail?["poster"]?["medium"]) ??
			root.SelectSingleNode("//div[contains(@class,'poster')]//img")?.GetAttributeValue("src", string.Empty) ??
			string.Empty;

		var description = Text(detail?["synopsis"]) ??
			Clean(root.SelectSingleNode("//*[contains(@class,'mpage__desc')]")?.InnerText);

		var authors = Titles(detail?["authors"]);
		var artists = Titles(detail?["artists"]);
		var groups = GetQuery(initialData, "manga", "groups", requestedId)?
			.AsArray()
			.Select(x => Text(x?["name"]))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray() ?? [];

		var tags = BuildTags(detail);
		var attributes = BuildMangaAttributes(detail);

		var manga = new ImportManga
		{
			Title = title,
			Id = sourceId,
			Provider = Provider,
			HomePage = homePage,
			Cover = AbsoluteUrl(cover, HomeUrl) ?? string.Empty,
			Description = description,
			AltDescriptions = [],
			AltTitles = Strings(detail?["altTitles"])
				.Append(title)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray(),
			Authors = authors,
			Artists = artists,
			Uploaders = groups,
			Rating = ParseContentRating(Text(detail?["contentRating"])),
			Chapters = ParseChapters(root),
			Attributes = attributes,
			Tags = tags,
			Referer = Referer,
			SourceCreated = Date(detail?["createdAt"]),
			OrdinalVolumeReset = false
		};

		manga.Nsfw = manga.Rating is not null && manga.Rating != ContentRating.Safe;

		return manga;
	}

	private static JsonNode? GetInitialData(HtmlNode root)
	{
		var node = root.SelectSingleNode("//script[@id='initial-data']");
		var json = HtmlEntity.DeEntitize(node?.InnerText ?? string.Empty);

		return string.IsNullOrWhiteSpace(json)
			? null
			: JsonNode.Parse(json);
	}

	private static JsonNode? GetQuery(JsonNode? initialData, params string[] parts)
	{
		var queries = initialData?["queries"]?.AsObject();
		if (queries is null)
			return null;

		var key = JsonSerializer.Serialize(parts);
		return queries.TryGetPropertyValue(key, out var value) ? value : null;
	}

	private List<ImportChapter> ParseChapters(HtmlNode root)
	{
		return root
			.SelectNodes("//li[contains(@class,'mchap-item')]")
			?.Select(ParseChapter)
			.Where(x => x is not null)
			.Cast<ImportChapter>()
			.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
			.Select(x => x.First())
			.OrderByDescending(x => x.Number)
			.ToList() ?? [];
	}

	private ImportChapter? ParseChapter(HtmlNode node, int index)
	{
		var primary = node.SelectSingleNode(".//a[contains(@class,'mchap-row__primary')]");
		var href = primary?.GetAttributeValue("href", null!);

		if (string.IsNullOrWhiteSpace(href))
			return null;

		var url = AbsoluteUrl(href, HomeUrl)!;
		var primaryText = Clean(primary?.InnerText) ?? string.Empty;
		var chapterId = ParseChapterId(url);
		var number = ParseChapterNumber(primaryText, url) ?? 0d;
		var groupNode = node.SelectSingleNode(".//a[contains(@class,'mchap-row__group')]");
		var group = Clean(groupNode?.InnerText);
		var groupUrl = AbsoluteUrl(groupNode?.GetAttributeValue("href", null!), HomeUrl);
		var likes = Clean(node.SelectSingleNode(".//*[contains(@class,'mchap-row__likes')]")?.InnerText);
		var time = Clean(node.SelectSingleNode(".//*[contains(@class,'mchap-row__time')]")?.InnerText);

		var attributes = new List<ImportAttribute>
		{
			new("source", "comix.to"),
			new("ordinal", index.ToString(CultureInfo.InvariantCulture))
		};

		Add(attributes, "chapterText", primaryText);
		Add(attributes, "group", group);
		Add(attributes, "groupUrl", groupUrl);
		Add(attributes, "likes", likes);
		Add(attributes, "time", time);

		return new ImportChapter
		{
			Title = primaryText,
			Url = url,
			Id = chapterId,
			Number = number,
			Volume = ParseVolume(primaryText, url),
			ExternalUrl = null,
			Language = "en",
			Attributes = attributes,
			LegacyId = int.TryParse(chapterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyId)
				? legacyId
				: null,
			Pages = []
		};
	}

	private static string[] BuildTags(JsonNode? detail)
	{
		return Titles(detail?["genres"])
			.Concat(Titles(detail?["demographics"]))
			.Concat(Titles(detail?["formats"]))
			.Concat(Titles(detail?["tags"]))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static List<ImportAttribute> BuildMangaAttributes(JsonNode? detail)
	{
		var attributes = new List<ImportAttribute>();

		//Add(attributes, "source", "comix.to");
		Add(attributes, "type", Text(detail?["type"]));
		Add(attributes, "status", Text(detail?["status"]));
		Add(attributes, "originalLanguage", Text(detail?["originalLanguage"]));
		Add(attributes, "latestChapter", Text(detail?["latestChapter"]));
		Add(attributes, "finalChapter", Text(detail?["finalChapter"]));
		Add(attributes, "finalVolume", Text(detail?["finalVolume"]));
		Add(attributes, "hasChapters", Text(detail?["hasChapters"]));
		Add(attributes, "chapterUpdatedAtFormatted", Text(detail?["chapterUpdatedAtFormatted"]));
		Add(attributes, "createdAtFormatted", Text(detail?["createdAtFormatted"]));
		Add(attributes, "updatedAtFormatted", Text(detail?["updatedAtFormatted"]));
		Add(attributes, "startDate", Text(detail?["startDate"]));
		Add(attributes, "endDate", Text(detail?["endDate"]));
		Add(attributes, "year", Text(detail?["year"]));
		Add(attributes, "rank", Text(detail?["rank"]));
		Add(attributes, "followsTotal", Text(detail?["followsTotal"]));
		Add(attributes, "ratedAvg", Text(detail?["ratedAvg"]));
		Add(attributes, "ratedCount", Text(detail?["ratedCount"]));
		Add(attributes, "contentRating", Text(detail?["contentRating"]));
		Add(attributes, "uploadUrl", Text(detail?["uploadUrl"]));
		Add(attributes, "editUrl", Text(detail?["editUrl"]));
		Add(attributes, "firstChapterUrl", Text(detail?["firstChapterUrl"]));
		Add(attributes, "latestChapterUrl", Text(detail?["latestChapterUrl"]));

		AddLinks(attributes, detail?["links"]);
		AddTagAttributes(attributes, "publisher", detail?["publishers"]);
		AddTagAttributes(attributes, "sourceEntry", detail?["sources"]);

		return attributes;
	}

	private static void AddLinks(List<ImportAttribute> attributes, JsonNode? links)
	{
		if (links is not JsonObject obj)
			return;

		foreach (var item in obj)
			Add(attributes, $"link:{item.Key}", Text(item.Value));
	}

	private static void AddTagAttributes(List<ImportAttribute> attributes, string prefix, JsonNode? values)
	{
		if (values is not JsonArray array)
			return;

		foreach (var item in array)
		{
			var id = Text(item?["id"]);
			var title = Text(item?["title"]);
			var slug = Text(item?["slug"]);

			if (!string.IsNullOrWhiteSpace(id))
				Add(attributes, $"{prefix}:id:{title ?? slug ?? id}", id);

			if (!string.IsNullOrWhiteSpace(slug))
				Add(attributes, $"{prefix}:slug:{title ?? id ?? slug}", slug);
		}
	}

	private static ContentRating? ParseContentRating(string? rating)
	{
		return rating?.Trim().ToLowerInvariant() switch
		{
			"safe" => ContentRating.Safe,
			"suggestive" => ContentRating.Suggestive,
			"erotica" => ContentRating.Erotica,
			"pornographic" => ContentRating.Pornographic,
			_ => null
		};
	}

	private static string ParseChapterId(string url)
	{
		var last = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? url;
		var match = Regex.Match(last, @"^(?<id>\d+)(?:-|$)", RegexOptions.IgnoreCase);

		return match.Success
			? match.Groups["id"].Value
			: last;
	}

	private static double? ParseChapterNumber(string text, string url)
	{
		var combined = $"{text} {url}";

		var match = Regex.Match(
			combined,
			@"(?:ch(?:apter)?\.?\s*|chapter-)(?<num>\d+(?:\.\d+)?)",
			RegexOptions.IgnoreCase);

		return match.Success &&
			double.TryParse(match.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
				? number
				: null;
	}

	private static double? ParseVolume(string text, string url)
	{
		var combined = $"{text} {url}";

		var match = Regex.Match(
			combined,
			@"(?:vol(?:ume)?\.?\s*|volume-)(?<num>\d+(?:\.\d+)?)",
			RegexOptions.IgnoreCase);

		return match.Success &&
			double.TryParse(match.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume)
				? volume
				: null;
	}

	private static string[] Titles(JsonNode? node)
	{
		return node is JsonArray array
			? array
				.Select(x => Text(x?["title"]) ?? Text(x?["name"]))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray()
			: [];
	}

	private static string[] Strings(JsonNode? node)
	{
		return node is JsonArray array
			? array
				.Select(Text)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray()
			: [];
	}

	private static int? Int(JsonNode? node)
	{
		var text = Text(node);

		return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: null;
	}

	private static DateTime? Date(JsonNode? node)
	{
		var text = Text(node);

		return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
			? value
			: null;
	}

	private static string? Text(JsonNode? node)
	{
		if (node is null)
			return null;

		var value = node.GetValueKind() switch
		{
			JsonValueKind.String => node.GetValue<string>(),
			JsonValueKind.Number => node.ToJsonString(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			_ => null
		};

		return Clean(value);
	}

	private static string? Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return HtmlEntity.DeEntitize(value).Trim();
	}

	private static void Add(List<ImportAttribute> attributes, string name, string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return;

		attributes.Add(new ImportAttribute(name, value));
	}

	private static string? AbsoluteUrl(string? url, string baseUrl)
	{
		if (string.IsNullOrWhiteSpace(url))
			return null;

		if (url.StartsWithIc("http"))
			return url;

		return $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
	}

	private sealed record ComixChapterPagination(
		int Start,
		int End,
		int TotalItems,
		int PageSize,
		int TotalPages);

	private static ComixChapterPagination? ParseChapterPagination(HtmlDocument doc)
	{
		var text = Clean(doc.DocumentNode
			.SelectSingleNode("//span[contains(@class,'mchap-foot__hint')]")
			?.InnerText);

		if (string.IsNullOrWhiteSpace(text))
			return null;

		var match = Regex.Match(
			text,
			@"Showing\s+(?<start>\d+)\s+to\s+(?<end>\d+)\s+of\s+(?<total>\d+)\s+items",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		if (!match.Success)
			return null;

		var start = int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture);
		var end = int.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture);
		var total = int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture);
		var pageSize = Math.Max(1, end - start + 1);
		var totalPages = (int)Math.Ceiling(total / (double)pageSize);

		return new ComixChapterPagination(
			Start: start,
			End: end,
			TotalItems: total,
			PageSize: pageSize,
			TotalPages: totalPages);
	}
	#endregion

	#region Chapter Page Parsing
	private ImportPage[] ParseChapterPages(HtmlDocument doc, string chapterUrl)
	{
		var pages = doc.DocumentNode
			.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' rpage-page ')][@data-page]")
			?.Select(x => ParseReaderPage(x, chapterUrl))
			.Where(x => x is not null)
			.Cast<ImportPage>()
			.OrderBy(GetOrdinal)
			.ToArray() ?? [];

		return pages;
	}

	private ImportPage? ParseReaderPage(HtmlNode pageNode, string chapterUrl)
	{
		var ordinal = IntAttr(pageNode, "data-page");
		if (ordinal is null or <= 0)
			return null;

		var imageNode = pageNode.SelectSingleNode(".//img[contains(concat(' ', normalize-space(@class), ' '), ' rpage-page__img ')]");

		var src =
			imageNode?.GetAttributeValue("src", null!) ??
			imageNode?.GetAttributeValue("data-src", null!) ??
			BestSrcSetUrl(imageNode?.GetAttributeValue("srcset", null!)) ??
			BestSrcSetUrl(imageNode?.GetAttributeValue("data-srcset", null!));

		var width = IntAttr(imageNode, "width");
		var height = IntAttr(imageNode, "height");

		if (width is null || height is null)
		{
			var dimensions = ParseAspectRatio(pageNode.GetAttributeValue("style", null!));
			width ??= dimensions.width;
			height ??= dimensions.height;
		}

		var pageUrl = AbsoluteUrl(src, chapterUrl);

		if (string.IsNullOrWhiteSpace(pageUrl))
		{
			pageUrl = GuessLazyPageUrl(pageNode, ordinal.Value, chapterUrl);
		}

		if (string.IsNullOrWhiteSpace(pageUrl))
			return null;

		return new ImportPage(pageUrl, width, height);
	}

	private static string? GuessLazyPageUrl(HtmlNode pageNode, int pageNumber, string chapterUrl)
	{
		var root = pageNode.OwnerDocument.DocumentNode;

		var knownImageUrls = root
			.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' rpage-page ')][@data-page]//img[@src]")
			?.Select(x => x.GetAttributeValue("src", null!))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => AbsoluteUrl(x, chapterUrl))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.ToArray() ?? [];

		if (knownImageUrls.Length == 0)
			return null;

		var first = knownImageUrls
			.Select(x => TryParseNumericImageUrl(x))
			.Where(x => x is not null)
			.Cast<NumericImageUrl>()
			.OrderBy(x => x.PageNumber)
			.FirstOrDefault();

		if (first is null)
			return null;

		var padded = first.Padding > 0
			? pageNumber.ToString($"D{first.Padding}", CultureInfo.InvariantCulture)
			: pageNumber.ToString(CultureInfo.InvariantCulture);

		return $"{first.Prefix}{padded}{first.Extension}{first.Query}";
	}

	private sealed record NumericImageUrl(
		string Prefix,
		int PageNumber,
		int Padding,
		string Extension,
		string Query);

	private static NumericImageUrl? TryParseNumericImageUrl(string url)
	{
		var match = Regex.Match(
			url,
			@"^(?<prefix>.*\/)(?<page>\d+)(?<ext>\.(?:avif|webp|jpe?g|png))(?<query>\?.*)?$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		if (!match.Success)
			return null;

		var pageText = match.Groups["page"].Value;

		if (!int.TryParse(pageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNumber))
			return null;

		var padding = pageText.StartsWith('0') ? pageText.Length : 0;

		return new NumericImageUrl(
			Prefix: match.Groups["prefix"].Value,
			PageNumber: pageNumber,
			Padding: padding,
			Extension: match.Groups["ext"].Value,
			Query: match.Groups["query"].Success ? match.Groups["query"].Value : string.Empty);
	}

	private static (int? width, int? height) ParseAspectRatio(string? style)
	{
		if (string.IsNullOrWhiteSpace(style))
			return (null, null);

		var match = Regex.Match(
			style,
			@"aspect-ratio\s*:\s*(?<width>\d+)\s*/\s*(?<height>\d+)",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		if (!match.Success)
			return (null, null);

		var width = int.TryParse(match.Groups["width"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
			? w
			: (int?)null;

		var height = int.TryParse(match.Groups["height"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
			? h
			: (int?)null;

		return (width, height);
	}

	private static int GetOrdinal(ImportPage page)
	{
		var ordinal = page.Headers
			.FirstOrDefault(x => string.Equals(x.Name, "ordinal", StringComparison.OrdinalIgnoreCase))
			?.Value;

		return int.TryParse(ordinal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: int.MaxValue;
	}

	private static int? IntAttr(HtmlNode? node, string attribute)
	{
		if (node is null)
			return null;

		var value = node.GetAttributeValue(attribute, null!);

		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
			? result
			: null;
	}

	private static string? BestSrcSetUrl(string? srcset)
	{
		if (string.IsNullOrWhiteSpace(srcset))
			return null;

		return srcset
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.Where(x => x.Length > 0)
			.Select(x => new
			{
				Url = x[0],
				Width = x.Length > 1 &&
					x[1].EndsWith("w", StringComparison.OrdinalIgnoreCase) &&
					int.TryParse(x[1][..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
						? width
						: 0
			})
			.OrderByDescending(x => x.Width)
			.Select(x => x.Url)
			.FirstOrDefault();
	}
	#endregion

	/// <summary>
	/// Unscrambles images that were scrambled using a seeded grid permutation.
	/// </summary>
	public static class ImageUnscrambler
	{
		public enum PermutationMode
		{
			/// <summary>
			/// Scrambled tile position i contains the original tile permutation[i].
			/// </summary>
			ScrambledPositionContainsOriginalIndex,

			/// <summary>
			/// Original tile position i was moved to scrambled tile position permutation[i].
			/// </summary>
			OriginalIndexMovedToScrambledPosition
		}

		public static async Task UnscrambleFileAsync(
			string inputPath,
			string outputPath,
			string scrambleSeedHeader,
			string? scrambleGridHeader = null,
			PermutationMode mode = PermutationMode.ScrambledPositionContainsOriginalIndex,
			CancellationToken token = default)
		{
			var seed = ParseSeed(scrambleSeedHeader);
			var (columns, rows) = ParseGrid(scrambleGridHeader);

			using var image = await Image.LoadAsync<Rgba32>(inputPath, token);
			using var output = Unscramble(image, seed, columns, rows, mode);

			await output.SaveAsync(outputPath, token);
		}

		public static Image Unscramble(
			Image<Rgba32> image, 
			string scrambleSeedHeader,
			string scrambleGridHeader,
			PermutationMode mode = PermutationMode.ScrambledPositionContainsOriginalIndex)
		{
			var seed = ParseSeed(scrambleSeedHeader);
			var (columns, rows) = ParseGrid(scrambleGridHeader);
			return Unscramble(image, seed, columns, rows, mode);
		}

		public static Image Unscramble(
			Image<Rgba32> scrambled,
			int seed,
			int columns,
			int rows,
			PermutationMode mode = PermutationMode.ScrambledPositionContainsOriginalIndex)
		{
			if (scrambled is null)
				throw new ArgumentNullException(nameof(scrambled));

			if (columns <= 0)
				throw new ArgumentOutOfRangeException(nameof(columns));

			if (rows <= 0)
				throw new ArgumentOutOfRangeException(nameof(rows));

			var tileWidth = scrambled.Width / columns;
			var tileHeight = scrambled.Height / rows;

			if (tileWidth <= 0 || tileHeight <= 0)
				throw new InvalidOperationException("Image is too small for the requested scramble grid.");

			var tileCount = columns * rows;
			var permutation = ScrambleRandom.CreatePermutation(seed, tileCount);

			// Clone instead of creating a blank image so any right/bottom remainder pixels
			// that were not part of the fixed-size scramble grid are preserved.
			var output = new Image<Rgba32>(scrambled.Width, scrambled.Height);
			CopyPixels(
				source: scrambled,
				sourceRect: new Rectangle(0, 0, scrambled.Width, scrambled.Height),
				destination: output,
				destinationPoint: Point.Empty);

			for (var scrambledIndex = 0; scrambledIndex < tileCount; scrambledIndex++)
			{
				var sourceIndex = mode switch
				{
					PermutationMode.ScrambledPositionContainsOriginalIndex => scrambledIndex,
					PermutationMode.OriginalIndexMovedToScrambledPosition => permutation[scrambledIndex],
					_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
				};

				var destinationIndex = mode switch
				{
					PermutationMode.ScrambledPositionContainsOriginalIndex => permutation[scrambledIndex],
					PermutationMode.OriginalIndexMovedToScrambledPosition => scrambledIndex,
					_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
				};

				var sourceRect = GetFixedTileRectangle(sourceIndex, tileWidth, tileHeight, columns);
				var destinationPoint = GetFixedTilePoint(destinationIndex, tileWidth, tileHeight, columns);

				CopyPixels(scrambled, sourceRect, output, destinationPoint);
			}

			return output;
		}

		private static Rectangle GetFixedTileRectangle(
			int index,
			int tileWidth,
			int tileHeight,
			int columns)
		{
			var column = index % columns;
			var row = index / columns;

			return new Rectangle(
				column * tileWidth,
				row * tileHeight,
				tileWidth,
				tileHeight);
		}

		private static Point GetFixedTilePoint(
			int index,
			int tileWidth,
			int tileHeight,
			int columns)
		{
			var column = index % columns;
			var row = index / columns;

			return new Point(
				column * tileWidth,
				row * tileHeight);
		}

		private static void CopyPixels(
			Image<Rgba32> source,
			Rectangle sourceRect,
			Image<Rgba32> destination,
			Point destinationPoint)
		{
			source.ProcessPixelRows(destination, (sourceAccessor, destinationAccessor) =>
			{
				for (var y = 0; y < sourceRect.Height; y++)
				{
					var sourceY = sourceRect.Y + y;
					var destinationY = destinationPoint.Y + y;

					if ((uint)sourceY >= (uint)source.Height)
						continue;

					if ((uint)destinationY >= (uint)destination.Height)
						continue;

					var sourceRow = sourceAccessor.GetRowSpan(sourceY);
					var destinationRow = destinationAccessor.GetRowSpan(destinationY);

					var sourceX = sourceRect.X;
					var destinationX = destinationPoint.X;
					var width = sourceRect.Width;

					if (sourceX < 0)
					{
						var offset = -sourceX;
						sourceX = 0;
						destinationX += offset;
						width -= offset;
					}

					if (destinationX < 0)
					{
						var offset = -destinationX;
						destinationX = 0;
						sourceX += offset;
						width -= offset;
					}

					width = Math.Min(width, source.Width - sourceX);
					width = Math.Min(width, destination.Width - destinationX);

					if (width <= 0)
						continue;

					sourceRow.Slice(sourceX, width).CopyTo(destinationRow.Slice(destinationX, width));
				}
			});
		}

		public static int ParseSeed(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException("Missing X-Scramble-Seed header.", nameof(value));

			if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
				throw new FormatException($"Invalid X-Scramble-Seed value: {value}");

			return seed;
		}

		public static (int Columns, int Rows) ParseGrid(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return (5, 5);

			value = value.Trim();

			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var single))
				return (single, single);

			var match = Regex.Match(value, @"^\s*(\d+)\s*[xX,\s]\s*(\d+)\s*$");
			if (!match.Success)
				throw new FormatException($"Invalid X-Scramble-Grid value: {value}");

			var columns = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			var rows = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

			if (columns <= 0 || rows <= 0)
				throw new FormatException($"Invalid X-Scramble-Grid value: {value}");

			return (columns, rows);
		}
	}

	/// <summary>
	/// Seeded pseudo-random generator extracted from the JavaScript.
	/// </summary>
	public sealed class ScrambleRandom
	{
		private uint _state;

		/// <summary>
		/// Creates a seeded random generator.
		/// </summary>
		public ScrambleRandom(int seed)
		{
			_state = unchecked((uint)seed);
		}

		/// <summary>
		/// Equivalent to JavaScript:
		/// state = Math.imul(state, 1664525) + 1013904223
		/// </summary>
		public uint NextUInt32()
		{
			_state = unchecked((_state * 1664525u) + 1013904223u);
			return _state;
		}

		/// <summary>
		/// Returns a deterministic value in the range [0, maxExclusive).
		/// </summary>
		public int NextInt32(int maxExclusive)
		{
			if (maxExclusive <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Value must be greater than zero.");

			return (int)(NextUInt32() % (uint)maxExclusive);
		}

		/// <summary>
		/// Recreates the seeded Fisher-Yates permutation from the JavaScript.
		/// </summary>
		public static int[] CreatePermutation(int seed, int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "Value cannot be negative.");

			var rng = new ScrambleRandom(seed);
			var values = Enumerable.Range(0, count).ToArray();

			for (var i = values.Length - 1; i > 0; i--)
			{
				var j = rng.NextInt32(i + 1);
				(values[i], values[j]) = (values[j], values[i]);
			}

			return values;
		}
	}
}