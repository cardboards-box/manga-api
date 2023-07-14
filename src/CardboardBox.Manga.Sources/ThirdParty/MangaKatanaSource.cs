namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangaKatanaSource : IMangaSource { }

public class MangaKatanaSource : IMangaKatanaSource
{
    public string HomeUrl => "https://mangakatana.com/";

    public string Provider => "mangakatana";

    private readonly IApiService _api;

    public MangaKatanaSource(IApiService api)
    {
        _api = api;
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return null;

        var manga = new DbManga
        {
            Title = doc.Attribute("//meta[@property='og:title']", "content") ?? string.Empty,
            SourceId = SourceHelper.IdFromUrl(url),
            Provider = Provider,
            Url = url,
            Cover = doc.Attribute("//div[@class='d-cell-medium media']/div[@class='cover']/img", "src") ?? string.Empty,
            Description = doc.InnerHtml("//div[@class='summary']/p") ?? string.Empty,
            Referer = HomeUrl
        };

        var meta = doc.DocumentNode.SelectNodes("//ul[@class='meta d-table']/li[@class='d-row-small']");
        foreach (var li in meta)
        {
            var clone = li.Copy();
            var label = clone.InnerText("//div[@class='d-cell-small label']")?.ToLower()?.Trim();

            switch (label)
            {
                case "alt name(s):":
                    manga.AltTitles = clone
                        .InnerText("//div[@class='alt_name']")?
                        .Split(';')
                        .Select(t => t.Trim())
                        .ToArray() ?? Array.Empty<string>();
                    continue;
                case "genres:":
                    manga.Tags = clone.SelectNodes("//a[@class='text_0']").Select(t => t.InnerText.Trim()).ToArray();
                    continue;
            }
        }

        var chaps = new List<DbMangaChapter>();
        var chapters = doc.DocumentNode.SelectNodes("//table[@class='uk-table uk-table-striped']/tbody/tr/td/div[@class='chapter']/a");
        var i = chapters.Count;
        foreach (var chap in chapters)
        {
            i--;
            var href = chap.GetAttributeValue("href", "");
            var name = chap.InnerText;

            chaps.Add(new DbMangaChapter
            {
                Title = name.Trim(),
                Url = href.Trim(),
                SourceId = SourceHelper.IdFromUrl(href),
                Ordinal = i
            });
        }

        return new ResolvedManga(manga, chaps.OrderBy(t => t.Ordinal).ToArray());
    }

    public async Task<string[]> Pages(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return Array.Empty<string>();

        return doc.DocumentNode
            .SelectNodes("//script")
            .Select(t => t.InnerHtml)
            .Where(t => t.Contains("thzq=["))
            .SelectMany(t =>
            {
                var sections = t.Split(new[] { "thzq=[" }, StringSplitOptions.RemoveEmptyEntries);
                if (sections.Length < 2) return Array.Empty<string>();

                return sections
                    .Last()
                    .Split(']')
                    .First()
                    .Split(new[] { ',', '\'', '\"' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToArray();
            }).ToArray();
    }

    public bool Match(string url) => url.ToLower().StartsWith(HomeUrl);
}
