namespace MangaBox.Providers.Sources;

public interface IBattwoSource : IMangaUrlSource { }

public class BattwoSource(
	IApiService _api) : BaseMangaSource<BattwoSource>, IBattwoSource
{
	public override string HomeUrl => "https://battwo.com/";

	public string MangaBaseUri => $"{HomeUrl}series/";

	public string ChapterUri => $"{HomeUrl}chapter/";

	public override string Provider => "battwo";

	public override string Name => "Battwo";

	public override bool Enabled => false;

	public async Task<ImportPage[]> ChapterPages(string url, CancellationToken token)
	{
		var doc = await _api.GetHtml(url, token: token);
		if (doc == null) return [];

		var chapterId = url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

		throw new NotImplementedException();
	}

	public override Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		return ChapterPages(ChapterUri + chapterId, token);
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWithIc("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await _api.GetHtml(url, token: token);
		if (doc == null) return null;

		var manga = new ImportManga
		{
			Title = doc.InnerText("//h3[@class='item-title']/a") ?? "",
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = [doc.Attribute("//div[@class='row detail-set']/div[@class='col-24 col-sm-8 col-md-6 attr-cover']/img", "src") ?? ""],
			Description = doc.InnerHtml("//div[@id='limit-height-body-summary']/div[@class='limit-html']") ?? "",
			AltTitles = (doc.InnerText("//div[@class='pb-2 alias-set line-b-f']") ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray(),
		};

		var tags = doc.DocumentNode.SelectNodes("//div[@class='attr-item']").Select(t => t.InnerText.Replace("\n", ""));
		foreach (var tag in tags)
		{
			var parts = tag.Split(':');
			if (parts.Length < 2) continue;

			var title = parts.First().ToLower();
			var rest = string.Join(":", parts.Skip(1));
			var split = rest.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();

			var asTags = (string name) => manga.Attributes.AddRange(split.Select(t => new ImportAttribute(name, t)));

			switch (title)
			{
				case "genres": manga.Tags = split; break;
				case "authors": asTags("Author"); break;
				case "artists": asTags("Artist"); break;
				case "original language": asTags("Original Language"); break;
				case "translated language": asTags("Translated Language"); break;
				case "upload status": asTags("Status"); break;
				case "original work": asTags("State"); break;
				case "year of release": asTags("Year"); break;
			}
		}

		manga.Nsfw = manga.Tags.Any(t => new[] { "Mature" }.Contains(t));
		var chaps = doc.DocumentNode.SelectNodes("//div[@class='main']/div/a");
		int num = chaps.Count;
		foreach (var chap in chaps)
		{
			var title = WebUtility.HtmlDecode(chap.InnerText.Replace("\n", ""));
			var uri = $"{HomeUrl}{chap.GetAttributeValue("href", "").Trim('/')}";

			manga.Chapters.Add(new()
			{
				Title = title,
				Url = uri,
				Number = num--,
				Id = uri.Split('/').Last()
			});
		}

		manga.Chapters = manga.Chapters.OrderBy(t => t.Number).ToList();

		return manga;
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWithIc(MangaBaseUri)) return (false, null);

		var id = url.Split('/').Last();
		return (true, id);
	}
}
