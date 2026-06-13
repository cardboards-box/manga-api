using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;
using Utilities.Flare;

public interface INhentaiNetSource : IMangaSource
{
	Task<NhentaiNetSearchResult[]> Search(string query, int page, CancellationToken token);

	Task<NhentaiNetSearchResult[]> Search(NhentaiNetQuery[] query, int page, CancellationToken token);
}

public sealed record NhentaiNetSearchResult(
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("url")] string Url,
	[property: JsonPropertyName("cover")] string? Cover);

public sealed record NhentaiNetQuery(
	[property: JsonPropertyName("criteria")] string Criteria,
	[property: JsonPropertyName("value")] string Value,
	[property: JsonPropertyName("negate")] bool Negate = false)
{
	public override string ToString() => $"{(Negate ? "-" : "")}{Criteria}:{Value.Replace(' ', '-')}";
}

public class NhentaiNetSource : BaseMangaSource<NhentaiNetSource>, INhentaiNetSource
{
	private const string DEFAULT_CHAPTER_TITLE = "Chapter 1";

	private readonly FlareSolverInstance _api;
	private readonly IConfiguration _config;
	private readonly ILogger<NhentaiNetSource> _logger;

	public NhentaiNetSource(
		IFlareSolverService flare,
		IConfiguration config,
		ILogger<NhentaiNetSource> logger)
	{
		_logger = logger;
		_config = config;
		_api = flare.Limiter();
		_api.DisableMedia = true;
	}

	public override string HomeUrl => "https://nhentai.net/";

	public string MangaBaseUri => $"{HomeUrl}g/";

	public override string Provider => "nhentai-net";

	public override string Name => "NHentai.net";

	public override TimeSpan IndexFrequency => TimeSpan.FromMinutes(5);

	public override bool IndexEnabled => false;

	public override bool UseProxiedImages => true;

	public override ContentRating? DefaultRating => ContentRating.Pornographic;

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var id = IdFromValue(mangaId) ?? IdFromValue(chapterId);
		if (string.IsNullOrWhiteSpace(id))
			return [];

		var gallery = await FetchGallery(id, token);
		return gallery is null ? [] : gallery.Pages;
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		id = IdFromValue(id) ?? id;
		var gallery = await FetchGallery(id, token);
		if (gallery is null)
			return null;

		var attributes = gallery.Attributes.ToList();
		if (!string.IsNullOrWhiteSpace(gallery.MediaId))
			attributes.Add(new("mediaId", gallery.MediaId));

		if (!string.IsNullOrWhiteSpace(gallery.Uploaded))
			attributes.Add(new("uploaded", gallery.Uploaded));

		return new ImportManga
		{
			Title = gallery.Title,
			Id = gallery.Id,
			Provider = Provider,
			HomePage = $"{MangaBaseUri}{gallery.Id}/",
			Cover = [gallery.Cover ?? string.Empty],
			Description = null,
			AltTitles = gallery.AltTitles,
			Tags = gallery.Tags,
			Rating = ContentRating.Pornographic,
			Nsfw = true,
			Referer = Referer,
			Attributes = attributes,
			Chapters =
			[
				new ImportChapter
				{
					Id = gallery.Id,
					Title = DEFAULT_CHAPTER_TITLE,
					Number = 1,
					Url = $"{MangaBaseUri}{gallery.Id}/",
					Volume = 1,
					Language = gallery.Language,
					Pages = [..gallery.Pages]
				}
			]
		};
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var id = IdFromValue(url);
		return string.IsNullOrWhiteSpace(id)
			? (false, null)
			: (true, id);
	}

	public override RateLimiter GetRateLimiter(string _) => _limiter ??= PolyfillExtensions.DefaultRateLimiter(10, 20);

	public override async IAsyncEnumerable<ImportManga> Index(LoaderSource source, [EnumeratorCancellation] CancellationToken token)
	{
		var searchLimiter = GetRateLimiter("search");
		var queries = IndexQueries();
		if (queries.Length == 0)
		{
			_logger.LogInformation("No NHentai.net index queries configured at Indexers:nHentaiNet:Queries.");
			yield break;
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var query in queries)
		{
			var page = 1;
			while (!token.IsCancellationRequested)
			{
				var lease = await searchLimiter.AcquireAsync(1, token);
				var results = await Search(query, page, token);
				lease.Dispose();
				if (results.Length == 0)
					break;

				foreach (var result in results)
				{
					token.ThrowIfCancellationRequested();

					var id = IdFromValue(result.Url);
					if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
						continue;

					lease = await GetRateLimiter(result.Url).AcquireAsync(1, token);
					var manga = await Manga(id, token);
					lease.Dispose();
					if (manga is null)
					{
						_logger.LogWarning("Failed to fetch NHentai.net indexed manga: {Url}", result.Url);
						continue;
					}

					yield return manga;
				}

				page++;
			}
		}
	}

	public async Task<NhentaiNetSearchResult[]> Search(string query, int page, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		page = Math.Max(1, page);
		var url = SearchUrl(query, page);

		try
		{
			var doc = await _api.GetHtml(url, token);
			return ParseSearchResults(doc);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to search NHentai.net: {Query}", query);
			return [];
		}
	}

	public async Task<NhentaiNetSearchResult[]> Search(NhentaiNetQuery[] query, int page, CancellationToken token)
	{
		if (query is null || query.Length == 0)
			return [];

		var queryString = string.Join(" ", query.Select(q => q.ToString()));

		page = Math.Max(1, page);
		var url = SearchUrl(queryString, page);

		try
		{
			var doc = await _api.GetHtml(url, token);
			return ParseSearchResults(doc);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to search NHentai.net: {Query}", queryString);
			return [];
		}
	}

	private string[] IndexQueries()
	{
		string[] sectionPaths =
		[
			"Indexers:nHentaiNet:Queries",
			"Indexers:NHentaiNet:Queries",
			"Indexers:nhentaiNet:Queries",
			"Indexers:nhentai-net:Queries",
			"Indexers:nhentai.net:Queries"
		];

		return [..sectionPaths
			.SelectMany(path => _config.GetSection(path).GetChildren())
			.Select(x => x.Value)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)];
	}

	private async Task<ParsedGallery?> FetchGallery(string id, CancellationToken token)
	{
		try
		{
			var doc = await _api.GetHtml($"{MangaBaseUri}{id}/", token);
			return ParseGallery(id, doc);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to fetch NHentai.net gallery: {GalleryId}", id);
			return null;
		}
	}

	private static ParsedGallery ParseGallery(string id, HtmlDocument doc)
	{
		var title = Clean(doc.InnerText("//div[@id='info']/h1"))
			?? CleanTitle(doc.Attribute("//meta[@property='og:title']", "content"))
			?? string.Empty;

		var altTitle = Clean(doc.InnerText("//div[@id='info']/h2"));
		var cover = NormalizeUrl(First(
			doc.Attribute("//div[@id='cover']//img", "data-src"),
			doc.Attribute("//div[@id='cover']//img", "src"),
			doc.Attribute("//meta[@property='og:image']", "content")));

		var tagLinks = doc.DocumentNode
			.SelectNodes("//span[contains(@class,'tags')]/a")
			?.ToArray() ?? [];

		var tags = TagNames(tagLinks, "tag");
		var language = TagNames(tagLinks, "language").FirstOrDefault()?.ToLowerInvariant() switch
		{
			"english" => "en",
			"japanese" => "ja",
			"chinese" => "zh",
			var value when !string.IsNullOrWhiteSpace(value) => value,
			_ => null
		};

		var attributes = tagLinks
			.Select(ParseAttribute)
			.Where(x => x is not null && !x.Name.Equals("tag", StringComparison.OrdinalIgnoreCase))
			.Cast<ImportAttribute>()
			.ToList();

		var uploaded = Clean(doc.InnerText("//time"))
			?? Clean(doc.DocumentNode.SelectSingleNode("//time")?.GetAttributeValue("datetime", null!));

		var galleryData = ParseGalleryData(doc);
		var pages = galleryData?.Pages is { Length: > 0 } dataPages
			? dataPages
			: ParsePages(doc);
		var mediaId = galleryData?.MediaId ?? pages
			.Select(x => ParseMediaId(x.Page))
			.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

		return new ParsedGallery(
			Id: id,
			Title: title,
			AltTitles: [..new[] { altTitle, title }
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.Distinct(StringComparer.OrdinalIgnoreCase)],
			Cover: cover,
			Tags: tags,
			Language: language,
			Attributes: attributes,
			Uploaded: uploaded,
			MediaId: mediaId,
			Pages: pages);
	}

	private static NhentaiNetSearchResult[] ParseSearchResults(HtmlDocument doc)
	{
		var nodes = doc.DocumentNode
			.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' gallery ')]")
			?.ToArray() ?? [];

		return [..nodes
			.Select(ParseSearchResult)
			.Where(x => x is not null)
			.Cast<NhentaiNetSearchResult>()];
	}

	private static NhentaiNetSearchResult? ParseSearchResult(HtmlNode gallery)
	{
		var anchor = gallery.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' cover ')]")
			?? gallery.SelectSingleNode(".//a[contains(@href, '/g/')]");
		var href = NormalizeMangaUrl(anchor?.GetAttributeValue("href", null!));
		if (string.IsNullOrWhiteSpace(href))
			return null;

		var title = Clean(gallery.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' caption ')]")?.InnerText)
			?? Clean(anchor?.GetAttributeValue("title", null!))
			?? string.Empty;

		var image = anchor?.SelectSingleNode(".//img") ?? gallery.SelectSingleNode(".//img");
		var cover = NormalizeUrl(First(
			image?.GetAttributeValue("data-src", null!),
			image?.GetAttributeValue("data-lazy-src", null!),
			image?.GetAttributeValue("src", null!)));

		return new NhentaiNetSearchResult(title, href, cover);
	}

	private static ImportPage[] ParsePages(HtmlDocument doc)
	{
		var dataPages = ParseGalleryData(doc)?.Pages ?? [];
		if (dataPages.Length > 0)
			return dataPages;

		var thumbs = doc.DocumentNode
			.SelectNodes("//div[contains(@class,'thumb-container')]//img")
			?.ToArray() ?? [];

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return [..thumbs
			.Select(x => NormalizeUrl(First(
				x.GetAttributeValue("data-src", null!),
				x.GetAttributeValue("data-lazy-src", null!),
				x.GetAttributeValue("src", null!))))
			.Select(FullImageUrlFromThumbnail)
			.Where(x => !string.IsNullOrWhiteSpace(x) && seen.Add(x))
			.Select((x, i) =>
			{
				var page = new ImportPage(x);
				page.Headers.Add(new("ordinal", (i + 1).ToString(CultureInfo.InvariantCulture)));
				return page;
			})];
	}

	private static GalleryData? ParseGalleryData(HtmlDocument doc)
	{
		foreach (var json in GalleryJson(doc))
		{
			var data = ParseGalleryJson(json);
			if (data?.Pages.Length > 0)
				return data;
		}

		return null;
	}

	private static IEnumerable<string> GalleryJson(HtmlDocument doc)
	{
		var apiData = doc.DocumentNode
			.SelectNodes("//script[@type='application/json' and contains(@data-url, '/api/v2/galleries/')]")
			?? Enumerable.Empty<HtmlNode>();
		foreach (var node in apiData)
		{
			var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
			if (string.IsNullOrWhiteSpace(text))
				continue;

			string? body = null;
			try
			{
				using var api = JsonDocument.Parse(text);
				if (api.RootElement.TryGetProperty("body", out var value) &&
					value.ValueKind == JsonValueKind.String)
					body = value.GetString();
			}
			catch
			{
				body = null;
			}

			if (!string.IsNullOrWhiteSpace(body))
				yield return body;
		}

		var data = doc.DocumentNode.SelectSingleNode("//script[@id='gallery-data']");
		var raw = Clean(data?.InnerText);
		if (!string.IsNullOrWhiteSpace(raw))
			yield return raw;

		var scripts = doc.DocumentNode.SelectNodes("//script") ?? Enumerable.Empty<HtmlNode>();
		foreach (var script in scripts)
		{
			var text = script.InnerText;
			if (string.IsNullOrWhiteSpace(text) || !text.Contains("_gallery", StringComparison.Ordinal))
				continue;

			var parseMatch = Regex.Match(
				text,
				@"window\._gallery\s*=\s*JSON\.parse\((?<quote>[""'])(?<json>(?:\\.|(?!\k<quote>).)*)\k<quote>\)",
				RegexOptions.Singleline | RegexOptions.CultureInvariant);
			if (parseMatch.Success)
			{
				var quote = parseMatch.Groups["quote"].Value;
				var encoded = parseMatch.Groups["json"].Value;
				var literal = $"{quote}{encoded}{quote}";
				string? decoded;
				try
				{
					decoded = JsonSerializer.Deserialize<string>(literal);
				}
				catch
				{
					decoded = null;
				}

				if (!string.IsNullOrWhiteSpace(decoded))
					yield return decoded;
			}

			var directMatch = Regex.Match(
				text,
				@"window\._gallery\s*=\s*(?<json>\{.+?\});",
				RegexOptions.Singleline | RegexOptions.CultureInvariant);
			if (directMatch.Success)
				yield return directMatch.Groups["json"].Value;
		}
	}

	private static GalleryData? ParseGalleryJson(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var mediaId = JsonString(root, "media_id");
			if (string.IsNullOrWhiteSpace(mediaId))
				return null;

			if (root.TryGetProperty("pages", out var directPages) &&
				directPages.ValueKind == JsonValueKind.Array)
				return ParseApiPages(mediaId, directPages);

			if (!root.TryGetProperty("images", out var images) ||
				!images.TryGetProperty("pages", out var pages) ||
				pages.ValueKind != JsonValueKind.Array)
				return null;

			var output = new List<ImportPage>();
			var ordinal = 1;
			foreach (var pageData in pages.EnumerateArray())
			{
				var ext = ImageExtension(JsonString(pageData, "t"));
				if (string.IsNullOrWhiteSpace(ext))
					continue;

				var width = JsonInt(pageData, "w");
				var height = JsonInt(pageData, "h");
				var page = new ImportPage($"{ImageCdnHost}/galleries/{mediaId}/{ordinal}.{ext}", width, height);
				page.Headers.Add(new("ordinal", ordinal.ToString(CultureInfo.InvariantCulture)));
				output.Add(page);
				ordinal++;
			}

			return new GalleryData(mediaId, [..output]);
		}
		catch
		{
			return null;
		}
	}

	private static GalleryData? ParseApiPages(string mediaId, JsonElement pages)
	{
		var output = new List<ImportPage>();
		foreach (var pageData in pages.EnumerateArray())
		{
			var path = JsonString(pageData, "path");
			if (string.IsNullOrWhiteSpace(path))
				continue;

			var ordinal = JsonInt(pageData, "number") ?? output.Count + 1;
			var width = JsonInt(pageData, "width");
			var height = JsonInt(pageData, "height");
			var page = new ImportPage(ImageUrlFromPath(path), width, height);
			page.Headers.Add(new("ordinal", ordinal.ToString(CultureInfo.InvariantCulture)));
			output.Add(page);
		}

		return output.Count == 0 ? null : new GalleryData(mediaId, [..output]);
	}

	private const string ImageCdnHost = "https://i2.nhentai.net";

	private static string ImageUrlFromPath(string path)
	{
		path = path.TrimStart('/');
		if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return path;

		return $"{ImageCdnHost}/{path}";
	}

	private static string? JsonString(JsonElement element, string property)
	{
		if (!element.TryGetProperty(property, out var value))
			return null;

		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString(),
			JsonValueKind.Number => value.GetRawText(),
			_ => null
		};
	}

	private static int? JsonInt(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) &&
			value.ValueKind == JsonValueKind.Number &&
			value.TryGetInt32(out var number)
				? number
				: null;
	}

	private static string? ImageExtension(string? value)
	{
		return value?.ToLowerInvariant() switch
		{
			"j" => "jpg",
			"p" => "png",
			"g" => "gif",
			"w" => "webp",
			"jpg" or "jpeg" => "jpg",
			"png" => "png",
			"gif" => "gif",
			"webp" => "webp",
			_ => null
		};
	}

	private static string FullImageUrlFromThumbnail(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return string.Empty;

		url = Regex.Replace(
			url,
			@"https?://t(?<host>\d*)\.nhentai\.net/",
			"https://i${host}.nhentai.net/",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return Regex.Replace(
			url,
			@"(?<page>\d+)t(?<ext>\.(?:jpe?g|png|gif|webp)(?:[?#].*)?)$",
			"${page}${ext}",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static ImportAttribute? ParseAttribute(HtmlNode anchor)
	{
		var type = TypeFromHref(anchor.GetAttributeValue("href", string.Empty));
		var value = Clean(anchor.SelectSingleNode(".//span[contains(@class,'name')]")?.InnerText)
			?? Clean(anchor.InnerText);

		return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value)
			? null
			: new ImportAttribute(type, value);
	}

	private static string[] TagNames(HtmlNode[] anchors, string type)
	{
		return [..anchors
			.Where(x => TypeFromHref(x.GetAttributeValue("href", string.Empty)).Equals(type, StringComparison.OrdinalIgnoreCase))
			.Select(x => Clean(x.SelectSingleNode(".//span[contains(@class,'name')]")?.InnerText) ?? Clean(x.InnerText))
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Cast<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)];
	}

	private static string TypeFromHref(string? href)
	{
		if (string.IsNullOrWhiteSpace(href))
			return string.Empty;

		if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
			href = uri.AbsolutePath;

		var parts = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
		return parts.FirstOrDefault() ?? string.Empty;
	}

	private static string? ParseMediaId(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return null;

		var match = Regex.Match(url, @"/galleries/(?<id>[^/]+)/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return match.Success ? match.Groups["id"].Value : null;
	}

	private static string? IdFromValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (int.TryParse(value.Trim('/'), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
			return value.Trim('/');

		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
			return null;

		if (!uri.Host.Equals("nhentai.net", StringComparison.OrdinalIgnoreCase))
			return null;

		var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 || !parts[0].Equals("g", StringComparison.OrdinalIgnoreCase))
			return null;

		return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
			? parts[1]
			: null;
	}

	private static string? NormalizeUrl(string? value)
	{
		value = Clean(value);
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (value.StartsWith("//"))
			return $"https:{value}";

		if (value.StartsWith('/'))
			return new Uri(new Uri("https://nhentai.net/"), value.TrimStart('/')).ToString();

		return value;
	}

	private static string? NormalizeMangaUrl(string? value)
	{
		var url = NormalizeUrl(value);
		if (string.IsNullOrWhiteSpace(url))
			return null;

		var id = IdFromValue(url);
		return string.IsNullOrWhiteSpace(id)
			? url
			: $"https://nhentai.net/g/{id}/";
	}

	private static string SearchUrl(string query, int page)
	{
		var encoded = Uri.EscapeDataString(query.Trim()).Replace("%20", "+", StringComparison.Ordinal);
		return $"https://nhentai.net/search/?q={encoded}&page={page.ToString(CultureInfo.InvariantCulture)}";
	}

	private static string? CleanTitle(string? value)
	{
		value = Clean(value);
		return value?
			.Replace("\u00bb nhentai: hentai doujinshi and manga", "", StringComparison.InvariantCultureIgnoreCase)
			.Replace("nhentai", "", StringComparison.InvariantCultureIgnoreCase)
			.Trim('-', '|', ':', ' ')
			.Trim();
	}

	private static string? Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = HtmlEntity.DeEntitize(value);
		value = Regex.Replace(value, @"\s+", " ");
		return value.Trim();
	}

	private static string? First(params string?[] values)
	{
		return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
	}

	private sealed record ParsedGallery(
		string Id,
		string Title,
		string[] AltTitles,
		string? Cover,
		string[] Tags,
		string? Language,
		List<ImportAttribute> Attributes,
		string? Uploaded,
		string? MediaId,
		ImportPage[] Pages);

	private sealed record GalleryData(string MediaId, ImportPage[] Pages);
}
