using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Utilities.Flare;
using static Services.MangaSource;

public interface IMangakakalotTvSource : IMangaUrlSource { }

public class MangakakalotTvSource(IFlareSolverService _flare) : IMangakakalotTvSource
{
	private static RateLimiter? _limiter;
	public string HomeUrl => "https://ww4.mangakakalot.tv/";

	public string ChapterBaseUri => $"{HomeUrl}chapter/";

	public string MangaBaseUri => $"{HomeUrl}manga/";

	public string Provider => "mangakakalot";

	public string Name => "Mangakakalot.tv";

	public string? Referer => null;

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => null;

	private readonly FlareSolverInstance _api = _flare.Limiter();

	public async Task<MangaChapterPage[]> ChapterPages(string url, CancellationToken token)
	{
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return [];

		return doc
				.DocumentNode
				.SelectNodes("//div[@class=\"vung-doc\"]/img[@class=\"img-loading\"]")
				.Select(t => new MangaChapterPage(t.GetAttributeValue("data-src", "")))
				.ToArray();
	}

	public Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{ChapterBaseUri}{mangaId}/{chapterId}";
		return ChapterPages(url, token);
	}

	public async Task<Manga?> Manga(string id, CancellationToken token)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return null;

		var manga = new Manga
		{
			Title = doc.DocumentNode.SelectSingleNode("//ul[@class=\"manga-info-text\"]/li/h1").InnerText,
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = HomeUrl + doc.DocumentNode.SelectSingleNode("//div[@class=\"manga-info-pic\"]/img").GetAttributeValue("src", "").TrimStart('/')
		};

		var desc = doc.DocumentNode.SelectSingleNode("//div[@id='noidungm']");
		foreach (var item in desc.ChildNodes.ToArray())
		{
			if (item.Name == "h2") item.Remove();
		}

		manga.Description = desc.InnerHtml;

		var textEntries = doc.DocumentNode.SelectNodes("//ul[@class=\"manga-info-text\"]/li");

		foreach (var li in textEntries)
		{
			if (!li.InnerText.StartsWith("Genres")) continue;

			var atags = li.ChildNodes.Where(t => t.Name == "a").Select(t => t.InnerText).ToArray();
			manga.Tags = atags;
			break;
		}

		var chapterEntries = doc.DocumentNode.SelectNodes("//div[@class=\"chapter-list\"]/div[@class=\"row\"]");

		int num = chapterEntries.Count;
		foreach (var chapter in chapterEntries)
		{
			var a = chapter.SelectSingleNode("./span/a");
			var href = HomeUrl + a.GetAttributeValue("href", "").TrimStart('/');
			var c = new MangaChapter
			{
				Title = a.InnerText.Trim(),
				Url = href,
				Number = num--,
				Id = href.Split('/').Last()
			};

			manga.Chapters.Add(c);
		}

		manga.Chapters = [.. manga.Chapters.OrderBy(t => t.Number)];

		return manga;
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.StartsWith(HomeUrl, StringComparison.CurrentCultureIgnoreCase);
		if (!matches) return (false, null);

		var parts = url[HomeUrl.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (domain.Equals("manga", StringComparison.CurrentCultureIgnoreCase) && parts.Length == 2) 
			return (true, parts.Last());

		if (domain.Equals("chapter", StringComparison.CurrentCultureIgnoreCase) && parts.Length > 1) 
			return (true, parts.Skip(1).First());

		return (false, null);
	}

	public RateLimiter GetRateLimiter() => _limiter ??= PolyfillExtensions.DefaultRateLimiter();
}
