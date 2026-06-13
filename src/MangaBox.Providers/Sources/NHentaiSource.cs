namespace MangaBox.Providers.Sources;

using Models.Types;
using Utilities.Flare;

public interface INhentaiSource : IMangaSource { }

public class NhentaiSource(IFlareSolverService _flare) : BaseMangaSource<NhentaiSource>, INhentaiSource
{
	private readonly FlareSolverInstance _api = _flare.Limiter();
	private const string DEFAULT_CHAPTER_TITLE = "Chapter 1";
	public override string HomeUrl => "https://nhentai.to/";

	public string MangaBaseUri => $"{HomeUrl}g/";

	public override string Provider => "nhentai";

	public override string Name => "NHentai.to";

	public override ContentRating? DefaultRating => ContentRating.Pornographic;

	public string FixPreview(string url)
	{
		var parts = url.Split('/');
		var fname = parts.Last();

		var ext = Path.GetExtension(fname);
		var fwext = Path.GetFileNameWithoutExtension(fname);
		if (fwext.EndsWith("t"))
			fwext = fwext.Substring(0, fwext.Length - 1);

		return string.Join('/', parts.SkipLast().Append($"{fwext}{ext}"));
	}

	public override async Task<ImportPage[]> ChapterPages(string id, string _, CancellationToken token)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return [];

		return doc.DocumentNode
				.SelectNodes("//div[@class='container']/div[@class='thumb-container']/a/img")
				.Select(t => FixPreview(t.GetAttributeValue("data-src", "").Trim()))
				.Select(t => new ImportPage(t))
				.ToArray();
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return null;

		var manga = new ImportManga
		{
			Title = doc.InnerText("//div[@id='info']/h1")?.Trim() ?? "",
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = [doc.Attribute("//div[@id='cover']/a/img", "src") ?? ""],
			Tags = doc.DocumentNode
					  .SelectNodes("//span[@class='tags']/a[contains(@href, '/tag')]/span[@class='name']")
					  .Select(t => t.InnerText.Trim())
					  .ToArray()
		};

		manga.Chapters.Add(new ImportChapter
		{
			Id = id,
			Title = DEFAULT_CHAPTER_TITLE,
			Number = 1,
			Url = url,
			Volume = 1
		});

		return manga;
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.ToLower().StartsWith(HomeUrl.ToLower());
		if (!matches) return (false, null);

		var parts = url.Remove(0, HomeUrl.Length).Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (domain.ToLower() != "g") return (false, null);

		if (parts.Length >= 2)
			return (true, parts[1]);

		return (false, null);
	}

}
