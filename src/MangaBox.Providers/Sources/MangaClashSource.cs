namespace MangaBox.Providers.Sources;

using Utilities.Flare;

public interface IMangaClashSource : IMangaSource { }

public class MangaClashSource(
	IFlareSolverService _flare) : BaseMangaSource<MangaClashSource>, IMangaClashSource
{
	public override string HomeUrl => "https://mangaclash.com/";

	public string MangaBaseUri => "https://mangaclash.com/manga/";

	public override string Provider => "mangaclash";

	public override string Name => "MangaClash";

	public override string? Referer => null;

	private readonly FlareSolverInstance _api = _flare.Limiter();

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{MangaBaseUri}{mangaId}/{chapterId}/";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return [];

		return doc.DocumentNode
				.SelectNodes("//div[@class='page-break no-gaps']/img")?
				.Select(t => new ImportPage(t.GetAttributeValue("data-src", "").Trim('\n', '\t', '\r')))
				.ToArray() ?? [];
	}

	public static string CleanTitle(string title)
	{
		return title
			.Replace("Read", "", StringComparison.InvariantCultureIgnoreCase)
			.Replace("Manga English [New Chapters] Online Free - ToonClash", "", StringComparison.InvariantCultureIgnoreCase)
			.Trim();
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return null;

		var manga = new ImportManga
		{
			Title = CleanTitle(doc.Attribute("//meta[@property='og:title']", "content") ?? ""),
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

		var chapters = doc.DocumentNode.SelectNodes("//li[contains(@class, 'wp-manga-chapter')]/a");
		int i = chapters.Count;
		foreach (var chap in chapters)
		{
			i--;
			var href = chap.GetAttributeValue("href", "");
			var name = chap.InnerText;

			manga.Chapters.Add(new ImportChapter
			{
				Title = name.Trim(),
				Url = href.Trim(),
				Id = href.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Last(),
				Number = i
			});
		}

		manga.Chapters = manga.Chapters.OrderBy(t => t.Number).ToList();

		return manga;
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
