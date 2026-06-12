using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;
using Utilities.Flare;

public interface INhentaiNetSource : IMangaSource { }

public class NhentaiNetSource : INhentaiNetSource, IRatedSource
{
	private static RateLimiter? _limiter;
	private const string DEFAULT_CHAPTER_TITLE = "Chapter 1";

	private readonly FlareSolverInstance _api;
	private readonly ILogger<NhentaiNetSource> _logger;

	public NhentaiNetSource(
		IFlareSolverService flare,
		ILogger<NhentaiNetSource> logger)
	{
		_logger = logger;
		_api = flare.Limiter();
		_api.DisableMedia = true;
	}

	public string HomeUrl => "https://nhentai.net/";

	public string MangaBaseUri => $"{HomeUrl}g/";

	public string Provider => "nhentai-net";

	public string Name => "NHentai.net";

	public string? Referer => HomeUrl;

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	public ContentRating DefaultRating => ContentRating.Pornographic;

	public async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var id = IdFromValue(mangaId) ?? IdFromValue(chapterId);
		if (string.IsNullOrWhiteSpace(id))
			return [];

		var gallery = await FetchGallery(id, token);
		return gallery is null ? [] : gallery.Pages;
	}

	public async Task<ImportManga?> Manga(string id, CancellationToken token)
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
					Language = gallery.Language
				}
			]
		};
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var id = IdFromValue(url);
		return string.IsNullOrWhiteSpace(id)
			? (false, null)
			: (true, id);
	}

	public RateLimiter GetRateLimiter(string _) => _limiter ??= PolyfillExtensions.DefaultRateLimiter();

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

		var pages = ParsePages(doc);
		var mediaId = pages
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

	private static ImportPage[] ParsePages(HtmlDocument doc)
	{
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

	private static string FullImageUrlFromThumbnail(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return string.Empty;

		url = url.Replace("https://t.nhentai.net/", "https://i.nhentai.net/", StringComparison.OrdinalIgnoreCase);
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

		if (value.StartsWith("//", StringComparison.Ordinal))
			return $"https:{value}";

		return value;
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
}
