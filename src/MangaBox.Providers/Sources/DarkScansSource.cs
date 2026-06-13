namespace MangaBox.Providers.Sources;

using Utilities.Flare;

public interface IDarkScansSource : IMangaSource { }

public class DarkScansSource(IFlareSolverService _flare) : BaseMangaSource<DarkScansSource>, IDarkScansSource
{
	private readonly FlareSolverInstance _api = _flare.Limiter();

	public override string HomeUrl => "https://dark-scan.com/";

	public string MangaBaseUri => $"{HomeUrl}manga/";

	public override string Provider => "dark-scans";

	public override string Name => "Dark Scans (dark-scans.com)";

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{MangaBaseUri}{mangaId}/{chapterId}/?style=list";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return [];

		return doc.DocumentNode
			.SelectNodes("//img[@class='wp-manga-chapter-img']")
			.Select(t => new ImportPage(t.GetAttributeValue("src", "").Trim('\n', '\t', '\r')))
			.ToArray();
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWithIc("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return null;

		var manga = new ImportManga
		{
			Title = doc.Attribute("//meta[@property='og:title']", "content") ?? "",
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = [doc.Attribute("//meta[@property='og:image']", "content") ?? ""]
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
		manga.Chapters = await GetChapters(url, token);

		return manga;
	}

	public async Task<List<ImportChapter>> GetChapters(string url, CancellationToken token)
	{
		//https://dark-scan.com/manga/yuusha-party-o-oida-sareta-kiyou-binbou/ajax/chapters/
		url += "/ajax/chapters";
		var doc = await _api.PostHtml(url, token);
		if (doc == null) return [];

		var output = new List<ImportChapter>();
		var chapters = doc.DocumentNode.SelectNodes("//li[contains(@class, 'wp-manga-chapter')]/a");
		int i = chapters.Count;
		foreach (var chap in chapters)
		{
			i--;
			var href = chap.GetAttributeValue("href", "");
			var name = chap.InnerText;

			output.Add(new ImportChapter
			{
				Title = name.Trim(),
				Url = href.Trim(),
				Id = href.Trim('/').Split('/').Last(),
				Number = i
			});
		}
		return [..output.OrderBy(t => t.Number)];
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.StartsWith(HomeUrl, StringComparison.CurrentCultureIgnoreCase);
		if (!matches) return (false, null);

		var parts = url[HomeUrl.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (!domain.Equals("manga", StringComparison.CurrentCultureIgnoreCase)) return (false, null);

		if (parts.Length >= 2)
			return (true, parts[1]);

		return (false, null);
	}
}
