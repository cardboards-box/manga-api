using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;

public interface IKappaBeastSource : IMangaSource { }

internal class KappaBeastSource(
	IApiService _api,
	IConfiguration _config) : IKappaBeastSource
{
	private static RateLimiter? _limiter;

	public string ApiKey => field ??= _config["KappaBeast:ApiKey"] ?? "d3cb8e38cd5dab862cf772d7912a57281b9d99cdae297372e21376d95fc5b956";

	public string HomeUrl => "https://kappabeast.com/";

	public string MangaBaseUri => $"{HomeUrl}series/";

	public string Provider => "kappa-beast";

	public string Name => "Kappa Beast (kappabeast.com)";

	public string? Referer => HomeUrl;

	public string? UserAgent => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 OPR/131.0.0.0";

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	public (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.StartsWith(HomeUrl, StringComparison.CurrentCultureIgnoreCase);
		if (!matches) return (false, null);

		var parts = url[HomeUrl.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (!domain.Equals("series", StringComparison.CurrentCultureIgnoreCase)) return (false, null);

		if (parts.Length >= 2)
			return (true, parts[1]);

		return (false, null);
	}

	public RateLimiter GetRateLimiter(string _) => _limiter ??= PolyfillExtensions.DefaultRateLimiter();

	#region Manga Fetching
	public async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var resp = await FetchManga(id, token);
		if (resp is null || resp.Data is null || resp.Data.Length == 0) return null;

		var manga = resp.Data.First();

		var media = manga.Media.FirstOrDefault();
		var cover = media?.CoverImage?.Formats?.Large?.Url ?? media?.CoverImage?.Url ?? media?.BannerImage?.Url;
		var categories = manga.Category
			.Select(t => t.Name)
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Select(t => t!.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		List<ImportChapter> chapters = [];
		if (!string.IsNullOrEmpty(manga.DocumentId))
			chapters = await PageChapters(manga.DocumentId, token).ToListAsync(token);

		return new ImportManga
		{
			Id = id,
			Title = manga.Title?.Trim() ?? string.Empty,
			Provider = Provider,
			HomePage = $"{MangaBaseUri}{id}",
			Cover = [KappaBeast.Url.AbsoluteCdn(cover) ?? string.Empty],
			Description = manga.Description,
			AltTitles = ParseAltTitles(manga.AltTitle),
			Authors = SplitName(manga.Author),
			Artists = SplitName(manga.Artist),
			Tags = categories,
			Rating = GetRating(categories),
			SourceCreated = manga.CreatedAt?.UtcDateTime,
			Referer = HomeUrl,
			Chapters = chapters,
			Attributes = [..new List<ImportAttribute>()
			{
				new("slug", manga.Slug ?? string.Empty),
				new("documentId", manga.DocumentId ?? string.Empty),
				new("id", manga.Id.ToString()),
				new("status", manga.MangaStatus ?? string.Empty),
				new("type", manga.Type ?? string.Empty),
				new("releaseYear", manga.ReleaseYear?.ToString() ?? string.Empty),
				new("publishedAt", manga.PublishedAt?.UtcDateTime.ToString("O") ?? string.Empty),
				new("updatedAt", manga.UpdatedAt?.UtcDateTime.ToString("O") ?? string.Empty)
			}.Where(t => !string.IsNullOrEmpty(t.Value))]
		};
	}

	private static string[] ParseAltTitles(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return [];

		return value
			.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string[] SplitName(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return [];

		return [value.Trim()];
	}

	private static ContentRating? GetRating(string[] categories)
	{
		if (categories.Any(t => t.Equals("Pornographic", StringComparison.OrdinalIgnoreCase)))
			return ContentRating.Pornographic;

		if (categories.Any(t => t.Equals("Erotica", StringComparison.OrdinalIgnoreCase)))
			return ContentRating.Erotica;

		if (categories.Any(t => t.Equals("Suggestive", StringComparison.OrdinalIgnoreCase) ||
								t.Equals("Ecchi", StringComparison.OrdinalIgnoreCase)))
			return ContentRating.Suggestive;

		return ContentRating.Safe;
	}

	public void AppendHeaders(IHttpBuilder bob)
	{
		bob.Message(c =>
		{
			c.Headers.Add("Accept", "application/json, text/plain, */*");
			c.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
			c.Headers.Add("Accept-Language", "en-US,en;q=0.9,ko;q=0.8,vi;q=0.7,ja;q=0.6,fr;q=0.5");
			c.Headers.Add("Origin", (Referer ?? string.Empty).TrimEnd('/'));
			c.Headers.Add("Pragma", "no-cache");
			c.Headers.Add("Priority", "u=1, i");
			c.Headers.Add("Referer", Referer ?? string.Empty);
			c.Headers.Add("Sec-Ch-Ua", "\"Opera GX\";v=\"131\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"147\"");
			c.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
			c.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
			c.Headers.Add("Sec-Fetch-Dest", "empty");
			c.Headers.Add("Sec-Fetch-Mode", "cors");
			c.Headers.Add("Sec-Fetch-Site", "same-site");
			c.Headers.Add("User-Agent", UserAgent ?? string.Empty);
			c.Headers.Add("X-API-Key", ApiKey);
		});
	}

	public async Task<KappaBeast.MangaResponse?> FetchManga(string title, CancellationToken token)
	{
		var url = KappaBeast.Url.AbsoluteApi("/api/content/mangas", new()
		{
			["filters[slug][$eq]"] = title,
			["populate[media][populate]"] = "*",
			["populate[category][fields][0]"] = "name",
			["pagination[pageSize]"] = "1",
		});

		if (string.IsNullOrEmpty(url))
			return null;

		return await _api.Get<KappaBeast.MangaResponse>(url, AppendHeaders, token);
	}
	#endregion

	#region Chapter Fetching
	public async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var manga = await FetchManga(mangaId, token);
		if (manga is null || manga.Data is null || manga.Data.Length == 0)
			return [];

		var docId = manga.Data.First().DocumentId;
		if (string.IsNullOrEmpty(docId)) return [];

		return (await PageChapters(docId, token)
			.Where(c => c.Id == chapterId)
			.FirstOrDefaultAsync(token))?.Pages.ToArray() ?? [];
	}

	public async Task<KappaBeast.ChapterResponse?> FetchChapter(string mangaId, int page, CancellationToken token)
	{
		var url = KappaBeast.Url.AbsoluteApi("/api/content/chapters", new()
		{
			["filters[manga][documentId][$eq]"] = mangaId,
			["populate[pages][populate]"] = "*",
			["populate"] = "manga",
			["sort[0]"] = "number:asc",
			["pagination[page]"] = page.ToString(),
			["pagination[pageSize]"] = "100",
		});

		if (string.IsNullOrEmpty(url))
			return null;

		return await _api.Get<KappaBeast.ChapterResponse>(url, AppendHeaders, token);
	}

	public async IAsyncEnumerable<ImportChapter> PageChapters(string mangaId, [EnumeratorCancellation] CancellationToken token)
	{
		var page = 1;
		while (true)
		{
			var resp = await FetchChapter(mangaId, page, token);
			if (resp is null || resp.Data is null || resp.Data.Length == 0)
				yield break;
			foreach (var chapter in resp.Data)
			{
				var converted = Convert(chapter);
				if (converted is not null)
					yield return converted;
			}
			if (resp.Meta?.Pagination is null || page >= resp.Meta.Pagination.PageCount)
				yield break;
			page++;
		}
	}

	public ImportChapter? Convert(KappaBeast.Chapter chapter)
	{
		if (chapter is null)
			return null;

		var id = chapter.DocumentId ?? chapter.Id.ToString();

		return new ImportChapter
		{
			Id = id,
			Title = chapter.Title,
			Url = ChapterUrl(chapter),
			Number = (double)chapter.Number,
			Volume = null,
			Language = "en",
			Pages = [.. ParseContent(chapter)],
			Attributes =
			[
				new("documentId", chapter.DocumentId ?? string.Empty),
				new("commentsCount", chapter.CommentsCount?.ToString() ?? string.Empty),
				new("createdAt", chapter.CreatedAt?.UtcDateTime.ToString("O") ?? string.Empty),
				new("updatedAt", chapter.UpdatedAt?.UtcDateTime.ToString("O") ?? string.Empty),
				new("publishedAt", chapter.PublishedAt?.UtcDateTime.ToString("O") ?? string.Empty),
				new("earlyAccessUntil", chapter.EarlyAccessUntil?.UtcDateTime.ToString("O") ?? string.Empty)
			]
		};
	}

	private string ChapterUrl(KappaBeast.Chapter chapter)
	{
		var manga = chapter.Manga;
		var baseUrl = HomeUrl?.TrimEnd('/');
		return $"{baseUrl}/reader/{manga?.Slug}/{chapter.Number}";
	}

	public static ImportPage[] ParseContent(KappaBeast.Chapter chapter)
	{
		var html = chapter.HtmlContent;
		if (string.IsNullOrWhiteSpace(html))
			return [];

		var doc = new HtmlDocument();
		doc.LoadHtml(html);

		var images = doc.DocumentNode.SelectNodes("//img");
		if (images is null || images.Count == 0)
			return [];

		var pages = new List<ImportPage>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var img in images)
		{
			var page = GetPageUrl(img);
			if (string.IsNullOrWhiteSpace(page))
				continue;

			page = WebUtility.HtmlDecode(page.Trim());

			if (!seen.Add(page))
				continue;

			var width = GetIntAttribute(img, "data-original-width")
				?? GetIntAttribute(img, "width");

			var height = GetIntAttribute(img, "data-original-height")
				?? GetIntAttribute(img, "height");

			pages.Add(new ImportPage(page, width, height));
		}

		return [.. pages];
	}

	private static string? GetPageUrl(HtmlNode img)
	{
		// Prefer the parent anchor URL because Blogger-style content often puts
		// the original/full-size image in <a href="..."> and a resized image in <img src="...">.
		var anchor = img.Ancestors("a").FirstOrDefault();
		var href = anchor?.GetAttributeValue("href", null!);

		if (IsUsableImageUrl(href))
			return href;

		var src = img.GetAttributeValue("src", null!);
		if (IsUsableImageUrl(src))
			return src;

		var dataSrc = img.GetAttributeValue("data-src", null!);
		if (IsUsableImageUrl(dataSrc))
			return dataSrc;

		var original = img.GetAttributeValue("data-original", null!);
		if (IsUsableImageUrl(original))
			return original;

		return null;
	}

	private static int? GetIntAttribute(HtmlNode node, string name)
	{
		var value = node.GetAttributeValue(name, null!);
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return int.TryParse(value, out var result)
			? result
			: null;
	}

	private static bool IsUsableImageUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return false;

		url = WebUtility.HtmlDecode(url.Trim());

		if (!HasHttpScheme(url))
			return false;

		var path = GetUrlPath(url);

		return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".avif", StringComparison.OrdinalIgnoreCase);
	}

	private static bool HasHttpScheme(string url)
	{
		return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
			   url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetUrlPath(string url)
	{
		var schemeIndex = url.IndexOf("://", StringComparison.Ordinal);
		if (schemeIndex < 0)
			return url;

		var pathStart = url.IndexOf('/', schemeIndex + 3);
		if (pathStart < 0)
			return string.Empty;

		var queryStart = url.IndexOfAny(['?', '#'], pathStart);
		if (queryStart < 0)
			return url[pathStart..];

		return url[pathStart..queryStart];
	}

	#endregion
}

/// <summary>
/// Contains models and helper methods for the api.kappabeast.com manga API.
/// </summary>
public static class KappaBeast
{
	/// <summary>
	/// The root response returned by the manga listing endpoint.
	/// </summary>
	public class MangaResponse
	{
		/// <summary>
		/// The manga records returned by the API.
		/// </summary>
		[JsonPropertyName("data")]
		public Manga[] Data { get; set; } = [];

		/// <summary>
		/// Metadata for the response, including pagination details.
		/// </summary>
		[JsonPropertyName("meta")]
		public Meta? Meta { get; set; }
	}

	/// <summary>
	/// Represents a manga entry returned by api.kappabeast.com.
	/// </summary>
	public class Manga
	{
		/// <summary>
		/// The numeric internal ID of the manga.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID for the manga.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }

		/// <summary>
		/// The primary title of the manga.
		/// </summary>
		[JsonPropertyName("title")]
		public string? Title { get; set; }

		/// <summary>
		/// The description or synopsis of the manga.
		/// </summary>
		[JsonPropertyName("description")]
		public string? Description { get; set; }

		/// <summary>
		/// The author of the manga.
		/// </summary>
		[JsonPropertyName("author")]
		public string? Author { get; set; }

		/// <summary>
		/// The publication status of the manga, such as <c>Ongoing</c> or <c>Completed</c>.
		/// </summary>
		[JsonPropertyName("manga_status")]
		public string? MangaStatus { get; set; }

		/// <summary>
		/// The UTC date and time when the manga record was created.
		/// </summary>
		[JsonPropertyName("createdAt")]
		public DateTimeOffset? CreatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the manga record was last updated.
		/// </summary>
		[JsonPropertyName("updatedAt")]
		public DateTimeOffset? UpdatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the manga record was published.
		/// </summary>
		[JsonPropertyName("publishedAt")]
		public DateTimeOffset? PublishedAt { get; set; }

		/// <summary>
		/// The media type of the entry, such as <c>Manga</c>.
		/// </summary>
		[JsonPropertyName("type")]
		public string? Type { get; set; }

		/// <summary>
		/// Alternative titles for the manga, usually stored as a comma-separated string.
		/// </summary>
		[JsonPropertyName("altTitle")]
		public string? AltTitle { get; set; }

		/// <summary>
		/// The original release year of the manga, when available.
		/// </summary>
		[JsonPropertyName("releaseYear")]
		public int? ReleaseYear { get; set; }

		/// <summary>
		/// The artist of the manga.
		/// </summary>
		[JsonPropertyName("artist")]
		public string? Artist { get; set; }

		/// <summary>
		/// The URL-safe slug used to identify the manga.
		/// </summary>
		[JsonPropertyName("slug")]
		public string? Slug { get; set; }

		/// <summary>
		/// The media components associated with the manga, including cover and banner images.
		/// </summary>
		[JsonPropertyName("media")]
		public MediaComponent[] Media { get; set; } = [];

		/// <summary>
		/// The categories or genres associated with the manga.
		/// </summary>
		[JsonPropertyName("category")]
		public Category[] Category { get; set; } = [];
	}

	/// <summary>
	/// Represents a category or genre assigned to a manga.
	/// </summary>
	public class Category
	{
		/// <summary>
		/// The numeric internal ID of the category.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID for the category.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }

		/// <summary>
		/// The display name of the category.
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }
	}

	/// <summary>
	/// Represents a Strapi media component attached to a manga.
	/// </summary>
	public class MediaComponent
	{
		/// <summary>
		/// The Strapi component name, such as <c>shared.media</c>.
		/// </summary>
		[JsonPropertyName("__component")]
		public string? Component { get; set; }

		/// <summary>
		/// The numeric internal ID of the media component.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The cover image associated with the manga.
		/// </summary>
		[JsonPropertyName("coverImage")]
		public Image? CoverImage { get; set; }

		/// <summary>
		/// The banner image associated with the manga.
		/// </summary>
		[JsonPropertyName("bannerImage")]
		public Image? BannerImage { get; set; }
	}

	/// <summary>
	/// Represents an uploaded image or file returned by the API.
	/// </summary>
	public class Image
	{
		/// <summary>
		/// The numeric internal ID of the image.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID for the image.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }

		/// <summary>
		/// The original file name of the image.
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>
		/// Alternative text describing the image, when provided.
		/// </summary>
		[JsonPropertyName("alternativeText")]
		public string? AlternativeText { get; set; }

		/// <summary>
		/// A caption describing the image, when provided.
		/// </summary>
		[JsonPropertyName("caption")]
		public string? Caption { get; set; }

		/// <summary>
		/// The width of the original image, in pixels.
		/// </summary>
		[JsonPropertyName("width")]
		public int? Width { get; set; }

		/// <summary>
		/// The height of the original image, in pixels.
		/// </summary>
		[JsonPropertyName("height")]
		public int? Height { get; set; }

		/// <summary>
		/// The generated/resized image formats available for this image.
		/// </summary>
		[JsonPropertyName("formats")]
		public ImageFormats? Formats { get; set; }

		/// <summary>
		/// The storage hash of the original image.
		/// </summary>
		[JsonPropertyName("hash")]
		public string? Hash { get; set; }

		/// <summary>
		/// The file extension of the original image, including the leading period.
		/// </summary>
		[JsonPropertyName("ext")]
		public string? Ext { get; set; }

		/// <summary>
		/// The MIME type of the original image.
		/// </summary>
		[JsonPropertyName("mime")]
		public string? Mime { get; set; }

		/// <summary>
		/// The reported file size of the original image.
		/// </summary>
		[JsonPropertyName("size")]
		public decimal? Size { get; set; }

		/// <summary>
		/// The relative or absolute URL of the original image.
		/// </summary>
		[JsonPropertyName("url")]
		public string? Url { get; set; }

		/// <summary>
		/// The preview URL for the image, when provided by the API.
		/// </summary>
		[JsonPropertyName("previewUrl")]
		public string? PreviewUrl { get; set; }

		/// <summary>
		/// The storage provider used for the image, such as <c>local</c>.
		/// </summary>
		[JsonPropertyName("provider")]
		public string? Provider { get; set; }

		/// <summary>
		/// Provider-specific metadata for the image, when available.
		/// </summary>
		[JsonPropertyName("provider_metadata")]
		public object? ProviderMetadata { get; set; }

		/// <summary>
		/// The UTC date and time when the image record was created.
		/// </summary>
		[JsonPropertyName("createdAt")]
		public DateTimeOffset? CreatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the image record was last updated.
		/// </summary>
		[JsonPropertyName("updatedAt")]
		public DateTimeOffset? UpdatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the image record was published.
		/// </summary>
		[JsonPropertyName("publishedAt")]
		public DateTimeOffset? PublishedAt { get; set; }
	}

	/// <summary>
	/// Represents the generated image formats for an uploaded image.
	/// </summary>
	public class ImageFormats
	{
		/// <summary>
		/// The large generated image format, when available.
		/// </summary>
		[JsonPropertyName("large")]
		public ImageFormat? Large { get; set; }

		/// <summary>
		/// The small generated image format, when available.
		/// </summary>
		[JsonPropertyName("small")]
		public ImageFormat? Small { get; set; }

		/// <summary>
		/// The medium generated image format, when available.
		/// </summary>
		[JsonPropertyName("medium")]
		public ImageFormat? Medium { get; set; }

		/// <summary>
		/// The thumbnail generated image format, when available.
		/// </summary>
		[JsonPropertyName("thumbnail")]
		public ImageFormat? Thumbnail { get; set; }
	}

	/// <summary>
	/// Represents a single generated image format, such as large, medium, small, or thumbnail.
	/// </summary>
	public class ImageFormat
	{
		/// <summary>
		/// The file extension of the generated image, including the leading period.
		/// </summary>
		[JsonPropertyName("ext")]
		public string? Ext { get; set; }

		/// <summary>
		/// The relative or absolute URL of the generated image.
		/// </summary>
		[JsonPropertyName("url")]
		public string? Url { get; set; }

		/// <summary>
		/// The storage hash of the generated image.
		/// </summary>
		[JsonPropertyName("hash")]
		public string? Hash { get; set; }

		/// <summary>
		/// The MIME type of the generated image.
		/// </summary>
		[JsonPropertyName("mime")]
		public string? Mime { get; set; }

		/// <summary>
		/// The file name of the generated image.
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>
		/// The storage path of the generated image, when provided.
		/// </summary>
		[JsonPropertyName("path")]
		public string? Path { get; set; }

		/// <summary>
		/// The reported file size of the generated image.
		/// </summary>
		[JsonPropertyName("size")]
		public decimal? Size { get; set; }

		/// <summary>
		/// The width of the generated image, in pixels.
		/// </summary>
		[JsonPropertyName("width")]
		public int? Width { get; set; }

		/// <summary>
		/// The height of the generated image, in pixels.
		/// </summary>
		[JsonPropertyName("height")]
		public int? Height { get; set; }

		/// <summary>
		/// The exact size of the generated image, in bytes.
		/// </summary>
		[JsonPropertyName("sizeInBytes")]
		public long? SizeInBytes { get; set; }
	}

	/// <summary>
	/// Represents response metadata returned by the API.
	/// </summary>
	public class Meta
	{
		/// <summary>
		/// Pagination details for the current API response.
		/// </summary>
		[JsonPropertyName("pagination")]
		public Pagination? Pagination { get; set; }
	}

	/// <summary>
	/// Represents pagination information for a paged API response.
	/// </summary>
	public class Pagination
	{
		/// <summary>
		/// The current page number.
		/// </summary>
		[JsonPropertyName("page")]
		public int Page { get; set; }

		/// <summary>
		/// The number of records requested per page.
		/// </summary>
		[JsonPropertyName("pageSize")]
		public int PageSize { get; set; }

		/// <summary>
		/// The total number of available pages.
		/// </summary>
		[JsonPropertyName("pageCount")]
		public int PageCount { get; set; }

		/// <summary>
		/// The total number of records matching the query.
		/// </summary>
		[JsonPropertyName("total")]
		public int Total { get; set; }
	}

	/// <summary>
	/// The root response returned by a Kappa Beast chapter endpoint.
	/// </summary>
	public class ChapterResponse
	{
		/// <summary>
		/// The chapter records returned by the API.
		/// </summary>
		[JsonPropertyName("data")]
		public Chapter[] Data { get; set; } = [];

		/// <summary>
		/// Metadata for the response, including pagination details.
		/// </summary>
		[JsonPropertyName("meta")]
		public Meta? Meta { get; set; }
	}

	/// <summary>
	/// Represents a chapter returned by api.kappabeast.com.
	/// </summary>
	public class Chapter
	{
		/// <summary>
		/// The numeric internal ID of the chapter.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID for the chapter.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }

		/// <summary>
		/// The chapter number.
		/// 
		/// This is decimal because the API may return non-integer chapter numbers.
		/// </summary>
		[JsonPropertyName("number")]
		public decimal Number { get; set; }

		/// <summary>
		/// The title of the chapter.
		/// </summary>
		[JsonPropertyName("title")]
		public string? Title { get; set; }

		/// <summary>
		/// The number of comments on the chapter, when provided.
		/// </summary>
		[JsonPropertyName("commentsCount")]
		public int? CommentsCount { get; set; }

		/// <summary>
		/// The UTC date and time when the chapter record was created.
		/// </summary>
		[JsonPropertyName("createdAt")]
		public DateTimeOffset? CreatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the chapter record was last updated.
		/// </summary>
		[JsonPropertyName("updatedAt")]
		public DateTimeOffset? UpdatedAt { get; set; }

		/// <summary>
		/// The UTC date and time when the chapter record was published.
		/// </summary>
		[JsonPropertyName("publishedAt")]
		public DateTimeOffset? PublishedAt { get; set; }

		/// <summary>
		/// The HTML content for the chapter.
		/// 
		/// This commonly contains image tags for the manga pages.
		/// </summary>
		[JsonPropertyName("htmlContent")]
		public string? HtmlContent { get; set; }

		/// <summary>
		/// The UTC date and time until which the chapter is in early access, when applicable.
		/// </summary>
		[JsonPropertyName("earlyAccessUntil")]
		public DateTimeOffset? EarlyAccessUntil { get; set; }

		/// <summary>
		/// Page data for the chapter, when provided by the API.
		/// 
		/// The provided samples currently return this as null, so this is kept flexible.
		/// </summary>
		[JsonPropertyName("pages")]
		public object? Pages { get; set; }

		/// <summary>
		/// The manga associated with the chapter, when included by the endpoint.
		/// 
		/// Reuses the existing <see cref="KappaBeast.Manga"/> model.
		/// </summary>
		[JsonPropertyName("manga")]
		public Manga? Manga { get; set; }
	}

	/// <summary>
	/// The root response returned by a Kappa Beast chapter-to-manga relation endpoint.
	/// </summary>
	public class ChapterMangaRelationResponse
	{
		/// <summary>
		/// The chapter-to-manga relation records returned by the API.
		/// </summary>
		[JsonPropertyName("data")]
		public ChapterMangaRelation[] Data { get; set; } = [];

		/// <summary>
		/// Metadata for the response, including pagination details.
		/// </summary>
		[JsonPropertyName("meta")]
		public Meta? Meta { get; set; }
	}

	/// <summary>
	/// Represents a lightweight chapter record with an optional manga reference.
	/// </summary>
	public class ChapterMangaRelation
	{
		/// <summary>
		/// The numeric internal ID of the chapter or relation record.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID for the chapter or relation record.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }

		/// <summary>
		/// The manga reference associated with this record, or null when no manga is linked.
		/// </summary>
		[JsonPropertyName("manga")]
		public DocumentReference? Manga { get; set; }
	}

	/// <summary>
	/// Represents a lightweight Strapi document reference containing only an ID and document ID.
	/// </summary>
	public class DocumentReference
	{
		/// <summary>
		/// The numeric internal ID of the referenced document.
		/// </summary>
		[JsonPropertyName("id")]
		public int Id { get; set; }

		/// <summary>
		/// The Strapi document ID of the referenced document.
		/// </summary>
		[JsonPropertyName("documentId")]
		public string? DocumentId { get; set; }
	}

	/// <summary>
	/// Provides URL helpers for Kappa Beast API resources.
	/// </summary>
	public static class Url
	{
		/// <summary>
		/// The base URL for the Kappa Beast API.
		/// </summary>
		public const string BaseUrl = "https://api.kappabeast.com";

		/// <summary>
		/// The base URL for the Kappa Beast CDN.
		/// </summary>
		public const string CdnUrl = "https://strapi.kappabeast.com";

		/// <summary>
		/// Converts a relative Kappa Beast resource URL into an absolute URL.
		/// </summary>
		/// <param name="url">The relative or absolute URL to normalize.</param>
		/// <param name="api">Whether to use the API base URL or the CDN base URL.</param>
		/// <param name="pars">The parameters for the request</param>
		/// <returns>
		/// The absolute URL, or <see langword="null"/> when <paramref name="url"/> is null, empty, or whitespace.
		/// </returns>
		public static string? Absolute(string? url, Dictionary<string, string?>? pars = null, bool api = true)
		{
			string? Url()
			{
				if (string.IsNullOrWhiteSpace(url))
					return null;

				if (url.StartsWithIc("http"))
					return url;

				var baseUrl = api ? BaseUrl : CdnUrl;
				return $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
			}

			url = Url();
			if (string.IsNullOrEmpty(url))
				return null;

			var parsStr = SerializeParameters(pars);
			if (string.IsNullOrEmpty(parsStr))
				return url;
			
			return $"{url.TrimEnd('?')}?{parsStr}";
		}

		/// <summary>
		/// Serializes the parameters for the URL
		/// </summary>
		/// <param name="pars">The parameters</param>
		/// <returns>The serialized parameters or null if there are none</returns>
		public static string? SerializeParameters(Dictionary<string, string?>? pars)
		{
			var pairs = pars?.Where(t => !string.IsNullOrEmpty(t.Value))
				.Select(t => $"{Uri.EscapeDataString(t.Key)}={Uri.EscapeDataString(t.Value!)}")
				?? [];
			if (!pairs.Any())
				return null;

			return string.Join('&', pairs);
		}

		/// <summary>
		/// Converts a relative Kappa Beast API URL into an absolute URL.
		/// </summary>
		/// <param name="url">The relative or absolute URL to normalize.</param>
		/// <param name="pars">The parameters for the request</param>
		/// <returns>The absolute URL, or <see langword="null"/> when <paramref name="url"/> is null, empty, or whitespace.</returns>
		public static string? AbsoluteApi(string? url, Dictionary<string, string?>? pars = null) => Absolute(url, pars, true);

		/// <summary>
		/// Converts a relative Kappa Beast CDN URL into an absolute URL.
		/// </summary>
		/// <param name="url">The relative or absolute URL to normalize.</param>
		/// <param name="pars">The parameters for the request</param>
		/// <returns>The absolute URL, or <see langword="null"/> when <paramref name="url"/> is null, empty, or whitespace.</returns>
		public static string? AbsoluteCdn(string? url, Dictionary<string, string?>? pars = null) => Absolute(url, pars, false);
	}
}
