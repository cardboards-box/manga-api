namespace MangaBox.Providers.Sources;

using MangaBox.Utilities.Flare;
using static Services.MangaSource;

public interface IDarkScansSource : IMangaSource { }

public class DarkScansSource(IFlareSolverService _flare) : IDarkScansSource
{
	public string HomeUrl => "https://dark-scan.com/";

	public string MangaBaseUri => "https://dark-scan.com/manga/";

	public string Provider => "dark-scans";

	public string? Referer => HomeUrl;

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	private readonly FlareSolverInstance _api = _flare.Limiter();

	public async Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId)
	{
		var url = $"{MangaBaseUri}{mangaId}/{chapterId}/?style=list";
		var doc = await _api.GetHtml(url);
		if (doc == null) return [];

		return doc.DocumentNode
			.SelectNodes("//img[@class='wp-manga-chapter-img']")
			.Select(t => new MangaChapterPage(t.GetAttributeValue("src", "").Trim('\n', '\t', '\r')))
			.ToArray();
	}

	public async Task<Manga?> Manga(string id)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url);
		if (doc == null) return null;

		var manga = new Manga
		{
			Title = doc.Attribute("//meta[@property='og:title']", "content") ?? "",
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = doc.Attribute("//meta[@property='og:image']", "content") ?? ""
		};

		var postContent = doc.DocumentNode.SelectNodes("//div[@class='post-content_item']");

		foreach (var div in postContent)
		{
			var clone = div.Copy();
			var title = clone.InnerText("//h5")?.Trim().ToLower();
			var content = clone.SelectSingleNode("//div[@class='summary-content']");
			if (string.IsNullOrEmpty(title)) continue;

			if (title.Contains("alternative"))
			{
				manga.AltTitles = content.InnerText.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
				continue;
			}

			if (title.Contains("genre"))
			{
				manga.Tags = content.SelectNodes("//a[@rel='tag']").Select(t => t.InnerText.Trim()).ToArray();
				continue;
			}
		}

		manga.Description = doc.InnerHtml("//div[@class='summary__content show-more']") ?? "";
		manga.Chapters = await GetChapters(url);

		return manga;
	}

	public async Task<List<MangaChapter>> GetChapters(string url)
	{
		//https://dark-scan.com/manga/yuusha-party-o-oida-sareta-kiyou-binbou/ajax/chapters/
		url += "/ajax/chapters";
		var doc = await _api.PostHtml(url);
		if (doc == null) return new();

		var output = new List<MangaChapter>();
		var chapters = doc.DocumentNode.SelectNodes("//li[contains(@class, 'wp-manga-chapter')]/a");
		int i = chapters.Count;
		foreach (var chap in chapters)
		{
			i--;
			var href = chap.GetAttributeValue("href", "");
			var name = chap.InnerText;

			output.Add(new MangaChapter
			{
				Title = name.Trim(),
				Url = href.Trim(),
				Id = href.Trim('/').Split('/').Last(),
				Number = i
			});
		}
		return output.OrderBy(t => t.Number).ToList();
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.ToLower().StartsWith(HomeUrl.ToLower());
		if (!matches) return (false, null);

		var parts = url.Remove(0, HomeUrl.Length).Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (domain.ToLower() != "manga") return (false, null);

		if (parts.Length >= 2)
			return (true, parts[1]);

		return (false, null);
	}
}
