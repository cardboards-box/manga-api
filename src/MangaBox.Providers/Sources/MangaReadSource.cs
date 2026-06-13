namespace MangaBox.Providers.Sources;

using Models.Types;

public interface IMangaReadSource : IMangaSource { }

internal class MangaReadSource(
	IApiService _api,
	ILogger<MangaReadSource> _logger) : BaseMangaSource<MangaReadSource>, IMangaReadSource
{
	public override string HomeUrl => "https://www.mangaread.org/";

	public string MangaBaseUri => $"{HomeUrl}manga/";

	public override string Provider => "mangaread";

	public override string Name => "MangaRead (mangaread.org)";

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = chapterId.StartsWithIc("http")
			? chapterId
			: $"{MangaBaseUri}{mangaId.Trim('/')}/{chapterId.Trim('/')}/";

		var doc = await GetHtml(url, token);
		return doc is null ? [] : ParsePages(doc);
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWithIc("http")
			? id
			: $"{MangaBaseUri}{id.Trim('/')}/";

		var doc = await GetHtml(url, token);
		if (doc is null)
			return null;

		var sourceId = MangaIdFromUrl(url) ?? id.Trim('/');
		var title = Clean(doc.InnerText("//div[contains(@class,'post-title')]//h1"))
			?? CleanTitle(doc.Attribute("//meta[@property='og:title']", "content"))
			?? string.Empty;

		if (string.IsNullOrWhiteSpace(title))
		{
			_logger.LogWarning("Could not find title for MangaRead id: {MangaId}", id);
			return null;
		}

		var details = ParseDetails(doc);
		var tags = details.GetValuesOrDefault("genre")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return new ImportManga
		{
			Id = sourceId,
			Title = title,
			Provider = Provider,
			HomePage = url,
			Cover = [BestCover(doc) ?? string.Empty],
			Description = Clean(doc.InnerText("//div[contains(@class,'summary__content')]")) ?? string.Empty,
			AltTitles = SplitAltTitles(details.GetText("alternative")),
			Authors = details.GetValuesOrDefault("author").ToArray(),
			Artists = details.GetValuesOrDefault("artist").ToArray(),
			Tags = tags,
			Rating = tags.Any(x => x.Equals("Smut", StringComparison.OrdinalIgnoreCase) ||
								   x.Equals("Ecchi", StringComparison.OrdinalIgnoreCase))
				? ContentRating.Suggestive
				: ContentRating.Safe,
			Nsfw = tags.Any(x => x.Equals("Smut", StringComparison.OrdinalIgnoreCase) ||
								 x.Equals("Ecchi", StringComparison.OrdinalIgnoreCase)),
			Referer = Referer,
			Attributes = BuildAttributes(details),
			Chapters = ParseChapters(doc)
		};
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var id = MangaIdFromUrl(url);
		return string.IsNullOrWhiteSpace(id)
			? (false, null)
			: (true, id);
	}

	private async Task<HtmlDocument?> GetHtml(string url, CancellationToken token)
	{
		try
		{
			return await _api.GetHtml(url, HeadersFor(url), token);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to retrieve MangaRead page: {Url}", url);
			return null;
		}
	}

	private Action<HttpRequestMessage> HeadersFor(string referer) => request =>
	{
		request.Headers.Referrer = new Uri(referer);
	};

	private static List<ImportChapter> ParseChapters(HtmlDocument doc)
	{
		var anchors = doc.DocumentNode
			.SelectNodes("//li[contains(@class,'wp-manga-chapter')]/a")
			?.ToArray() ?? [];

		var fallback = anchors.Length;
		return [..anchors
			.Select(a =>
			{
				var title = Clean(a.InnerText) ?? string.Empty;
				var url = CleanUrl(a.GetAttributeValue("href", string.Empty));
				var id = url.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? title;
				var number = ExtractChapterNumber(title);

				return new ImportChapter
				{
					Title = title,
					Url = url,
					Id = id,
					Number = number ?? fallback--,
					Language = "en",
					Attributes = ParseChapterAttributes(a)
				};
			})
			.OrderBy(x => x.Number)];
	}

	private static List<ImportAttribute> ParseChapterAttributes(HtmlNode anchor)
	{
		var item = anchor.Ancestors("li").FirstOrDefault();
		var date = Clean(item?.SelectSingleNode(".//*[contains(@class,'chapter-release-date')]")?.InnerText);

		return string.IsNullOrWhiteSpace(date)
			? []
			: [new ImportAttribute("Date", date)];
	}

	private static ImportPage[] ParsePages(HtmlDocument doc)
	{
		var images = doc.DocumentNode
			.SelectNodes("//div[contains(@class,'reading-content')]//img[contains(@class,'wp-manga-chapter-img')] | //img[contains(@class,'wp-manga-chapter-img')]")
			?.ToArray() ?? [];

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return [..images
			.Select(GetImageUrl)
			.Select(CleanUrl)
			.Where(x => !string.IsNullOrWhiteSpace(x) && seen.Add(x))
			.Select((x, i) =>
			{
				var page = new ImportPage(x);
				page.Headers.Add(new("ordinal", (i + 1).ToString(CultureInfo.InvariantCulture)));
				return page;
			})];
	}

	private static string? GetImageUrl(HtmlNode image)
	{
		return First(
			image.GetAttributeValue("data-src", null!),
			image.GetAttributeValue("data-lazy-src", null!),
			image.GetAttributeValue("src", null!));
	}

	private static Dictionary<string, List<string>> ParseDetails(HtmlDocument doc)
	{
		var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		var items = doc.DocumentNode.SelectNodes("//div[contains(@class,'post-content_item')]") ?? Enumerable.Empty<HtmlNode>();

		foreach (var item in items)
		{
			var heading = Clean(item.SelectSingleNode(".//h5")?.InnerText)?.ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(heading))
				continue;

			var key = DetailKey(heading);
			if (key is null)
				continue;

			var content = item.SelectSingleNode(".//div[contains(@class,'summary-content')]");
			var values = content?
				.SelectNodes(".//a")
				?.Select(x => Clean(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.ToList() ?? [];

			if (values.Count == 0)
			{
				var text = Clean(content?.InnerText);
				if (!string.IsNullOrWhiteSpace(text))
					values.Add(text);
			}

			if (values.Count > 0)
				output[key] = values;
		}

		return output;
	}

	private static string? DetailKey(string heading)
	{
		if (heading.Contains("alternative")) return "alternative";
		if (heading.Contains("author")) return "author";
		if (heading.Contains("artist")) return "artist";
		if (heading.Contains("genre")) return "genre";
		if (heading.Contains("type")) return "type";
		if (heading.Contains("status")) return "status";
		if (heading.Contains("release")) return "release";
		if (heading.Contains("rating")) return "rating";
		return null;
	}

	private static List<ImportAttribute> BuildAttributes(Dictionary<string, List<string>> details)
	{
		return [..details
			.Where(x => x.Key is "type" or "status" or "release" or "rating")
			.Select(x => new ImportAttribute(x.Key, string.Join(", ", x.Value)))
			.Where(x => !string.IsNullOrWhiteSpace(x.Value))];
	}

	private static string? BestCover(HtmlDocument doc)
	{
		var img = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'summary_image')]//img");
		return CleanUrl(First(
			PickLargestFromSrcset(img?.GetAttributeValue("srcset", null!)),
			img?.GetAttributeValue("data-src", null!),
			img?.GetAttributeValue("src", null!),
			doc.Attribute("//meta[@property='og:image']", "content")));
	}

	private static string[] SplitAltTitles(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? []
			: [..value
				.Split([';', ',', '/', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Distinct(StringComparer.OrdinalIgnoreCase)];
	}

	private static double? ExtractChapterNumber(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = Regex.Match(value, @"\b(?:chapter|ch)\s*(?<num>\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase);
		if (!match.Success)
			match = Regex.Match(value, @"\b(?<num>\d+(?:\.\d+)?)\b");

		return match.Success &&
			double.TryParse(match.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
				? number
				: null;
	}

	private static string? PickLargestFromSrcset(string? srcset)
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

	private static string? MangaIdFromUrl(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
			return null;

		if (!uri.Host.Equals("www.mangaread.org", StringComparison.OrdinalIgnoreCase) &&
			!uri.Host.Equals("mangaread.org", StringComparison.OrdinalIgnoreCase))
			return null;

		var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 || !parts[0].Equals("manga", StringComparison.OrdinalIgnoreCase))
			return null;

		return parts[1];
	}

	private static string? CleanTitle(string? value)
	{
		value = Clean(value);
		return value?
			.Replace("Read", "", StringComparison.InvariantCultureIgnoreCase)
			.Replace("manga Online in English", "", StringComparison.InvariantCultureIgnoreCase)
			.Replace("Manga Online in English", "", StringComparison.InvariantCultureIgnoreCase)
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

	private static string CleanUrl(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		value = HtmlEntity.DeEntitize(value);
		value = Regex.Replace(value, @"\s+", string.Empty);
		return value.Trim();
	}

	private static string? First(params string?[] values)
	{
		return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
	}
}
