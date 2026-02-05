using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Utilities.Flare;
using static Services.MangaSource;

public interface ILikeMangaSource : IMangaUrlSource { }

//Not gonna lie, like 90% of this is chatGPT because
//I got lazy and wanted to see how well it would do.
internal class LikeMangaSource(
	IApiService _api,
	IFlareSolverService _flare,
	ILogger<LikeMangaSource> _logger) : ILikeMangaSource
{
	public string HomeUrl => "https://likemanga.in";

	public string Provider => "like-manga";

	public string Name => "LikeManga (likemanga.in)";

	public string MangaBaseUri => $"{HomeUrl}/manga/";

	public string? Referer => HomeUrl + "/";

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	private readonly FlareSolverInstance _flareInstance = _flare.Limiter();

	public async Task<MangaChapterPage[]> ChapterPages(string url, CancellationToken token)
	{
		var doc = await _flareInstance.GetHtml(url, token);
		if (doc is null) return [];

		return Parse(doc, url);
	}

	public Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{MangaBaseUri}{mangaId}/{chapterId}";
		return ChapterPages(url, token);
	}

	public async Task<Manga?> Manga(string id, CancellationToken token)
	{
		const string TitleXPath = "//div[contains(@class,'post-title')]/h1";
		const string GenresXPath = "//div[contains(@class,'post-content_item')][.//h5[normalize-space()='Genre(s)']]//div[contains(@class,'genres-content')]//a";
		const string SummaryParasXPath = "//div[contains(@class,'description-summary')]//div[contains(@class,'summary__content')]//p";
		const string CoverImgXPath = "//div[contains(@class,'summary_image')]//img";

		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _flareInstance.GetHtml(url, token);
		if (doc == null) return null;

		var title = SelectText(doc, TitleXPath);
		if (string.IsNullOrEmpty(title))
		{
			_logger.LogWarning("Could not find title for {id} >> {manga}", id, url);
			return null;
		}

		var genres = SelectNodes(doc, GenresXPath)
			.Select(n => Clean(n.InnerText))
			.Where(s => s.Length > 0).ToArray();
		var summary = JoinParagraphs(SelectNodes(doc, SummaryParasXPath));
		var img = doc.DocumentNode.SelectSingleNode(CoverImgXPath);
		var imageUrl = img is not null
			? PickLargestFromSrcset(img.GetAttributeValue("srcset", ""))
			?? img.GetAttributeValue("src", "")
			: null;
		var manga = new Manga
		{
			Title = title,
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = imageUrl ?? string.Empty,
			Description = summary,
			Tags = genres,
		};

		//https://likemanga.in/manga/i-got-my-wish-and-reincarnated-as-the-villainess-last-boss/ajax/chapters/
		var chapters = await _api.PostHtml($"{url.TrimEnd('/')}/ajax/chapters/", token: token);
		if (chapters is null)
		{
			_logger.LogWarning("Could not get chapters for manga: {url}", url);
			return null;
		}

		manga.Chapters = [.. ParseChapters(chapters)];

		return manga;
	}

	public static MangaChapter[] ParseChapters(HtmlDocument doc)
	{
		const string ChapterLiXPath = "//div[contains(@class,'listing-chapters_wrap')]//li[contains(@class,'wp-manga-chapter')]/a";
		var chapters = new List<MangaChapter>();

		var anchors = (doc.DocumentNode.SelectNodes(ChapterLiXPath) ?? Enumerable.Empty<HtmlNode>()).ToArray();

		foreach (var a in anchors)
		{
			var title = Clean(a?.InnerText);
			var url = a?.GetAttributeValue("href", "") ?? "";
			var id = url.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
			var number = ExtractChapterNumber(title);

			chapters.Add(new MangaChapter
			{
				Title = title,
				Url = url,
				Id = id,
				Number = double.IsNaN(number) ? anchors.Length - chapters.Count + 1 : number
			});
		}

		return [.. chapters.OrderBy(t => t.Number)];
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var parts = url.Remove(0, HomeUrl.Length)
			.Split('/', StringSplitOptions.RemoveEmptyEntries)
			.Where(t => !t.Equals("manga", StringComparison.InvariantCultureIgnoreCase))
			.ToArray();
		if (parts.Length == 0) return (false, null);

		return (true, parts.First());
	}

	private static IEnumerable<HtmlNode> SelectNodes(HtmlDocument doc, string xpath) =>
		doc.DocumentNode.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>();

	private static string SelectText(HtmlDocument doc, string xpath)
	{
		var node = doc.DocumentNode.SelectSingleNode(xpath);
		return node == null ? "" : Clean(node.InnerText);
	}

	private static string JoinParagraphs(IEnumerable<HtmlNode> paras)
	{
		var parts = paras
			.Select(p => Clean(p.InnerText))
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.ToList();

		// Single block with spaces is usually best for summaries
		return string.Join(" ", parts);
	}

	private static string Clean(string? s)
	{
		if (string.IsNullOrEmpty(s)) return "";
		// Decode HTML entities and collapse whitespace
		s = HtmlEntity.DeEntitize(s);
		s = Regex.Replace(s, @"\s+", " ").Trim();
		return s;
	}

	// Parses a srcset and returns the URL with the largest width descriptor.
	private static string? PickLargestFromSrcset(string srcset)
	{
		if (string.IsNullOrWhiteSpace(srcset)) return null;

		// Example: "url1 193w, url2 125w"
		// Split on commas, parse width descriptors, pick largest
		var best = srcset
			.Split(',')
			.Select(part => part.Trim())
			.Select(part =>
			{
				// part like: "<url> 193w" or "<url> 2x"
				var spaceIdx = part.LastIndexOf(' ');
				if (spaceIdx <= 0) return (url: part, width: 0);

				var url = part[..spaceIdx].Trim();
				var descriptor = part[(spaceIdx + 1)..].Trim().ToLowerInvariant();

				int width = 0;
				if (descriptor.EndsWith("w"))
				{
					int.TryParse(descriptor.TrimEnd('w'), out width);
				}
				else if (descriptor.EndsWith("x"))
				{
					// fallback: treat DPR as width-ish (not exact, but allows choosing the largest DPR if widths absent)
					if (double.TryParse(descriptor.TrimEnd('x'), out var dpr))
						width = (int)(dpr * 10000); // arbitrary scale for comparison
				}

				return (url, width);
			})
			.OrderByDescending(x => x.width)
			.FirstOrDefault();

		return string.IsNullOrWhiteSpace(best.url) ? null : best.url;
	}

	private static string CleanUrl(string? s)
	{
		if (string.IsNullOrWhiteSpace(s)) return string.Empty;
		// remove newlines/tabs and extra spaces around the URL in src attributes
		s = HtmlEntity.DeEntitize(s);
		s = s.Replace("\n", "").Replace("\r", "").Replace("\t", "");
		s = Regex.Replace(s, @"\s+", "");
		return s.Trim();
	}

	// XPaths we can rely on in this theme/page
	private const string H1_ChapterHeading = "//h1[@id='chapter-heading']";
	private const string CrumbActive = "//ol[contains(@class,'breadcrumb')]//li[contains(@class,'active')]";
	private const string PageImgs = "//div[contains(@class,'reading-content')]//img[contains(@class,'wp-manga-chapter-img')]";
	private const string Scripts = "//script";

	// Entry point
	public static MangaChapterPage[] Parse(HtmlDocument doc, string url)
	{
		return doc.DocumentNode
			.SelectNodes(PageImgs)?
			.Select(n => CleanUrl(n.GetAttributeValue("src", "")))
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.Select(t => new MangaChapterPage(t))
			.ToArray()
			?? [];
	}

	private static string? ExtractJsonString(string text, string pattern)
	{
		var m = Regex.Match(text ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
		if (m.Success)
		{
			// Unescape common JSON escapes
			return Regex.Replace(m.Groups[1].Value, @"\\/", "/");
		}
		return null;
	}

	private static double ExtractChapterNumber(string? source)
	{
		if (string.IsNullOrWhiteSpace(source)) return double.NaN;
		// Matches "Chapter 72" or "chapter-72" or "ch-72.5"
		var m = Regex.Match(source, @"(?:chapter[\s\-]*)([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
		if (!m.Success) m = Regex.Match(source, @"\b([0-9]+(?:\.[0-9]+)?)\b"); // last resort
		return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture, out var d)
				? d : double.NaN;
	}

	public RateLimiter GetRateLimiter() => PolyfillExtensions.DefaultRateLimiter();
}
