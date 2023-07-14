namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangakakalotTvSource : IMangaSource { }

public class MangakakalotTvSource : IMangakakalotTvSource
{
    public string HomeUrl => "https://ww4.mangakakalot.tv/";

    public string ChapterBaseUri => $"{HomeUrl}chapter/";

    public string MangaBaseUri => $"{HomeUrl}manga/";

    public string Provider => "mangakakalot";

    private readonly IApiService _api;

    public MangakakalotTvSource(IApiService api)
    {
        _api = api;
    }

    public async Task<string[]> Pages(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return Array.Empty<string>();

        return doc
            .DocumentNode
            .SelectNodes("//div[@class=\"vung-doc\"]/img[@class=\"img-loading\"]")
            .Select(t => t.GetAttributeValue("data-src", ""))
            .ToArray();
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return null;

        var manga = new DbManga
        {
            Title = doc.DocumentNode.SelectSingleNode("//ul[@class=\"manga-info-text\"]/li/h1").InnerText,
            SourceId = SourceHelper.IdFromUrl(url),
            Provider = Provider,
            Url = url,
            Cover = HomeUrl + doc.DocumentNode.SelectSingleNode("//div[@class=\"manga-info-pic\"]/img").GetAttributeValue("src", "").TrimStart('/'),
            Referer = HomeUrl
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

        var chaps = new List<DbMangaChapter>();

        int num = chapterEntries.Count;
        foreach (var chapter in chapterEntries)
        {
            var a = chapter.SelectSingleNode("./span/a");
            var href = HomeUrl + a.GetAttributeValue("href", "").TrimStart('/');
            var c = new DbMangaChapter
            {
                Title = a.InnerText.Trim(),
                Url = href,
                Ordinal = num--,
                SourceId = href.Split('/').Last()
            };

            chaps.Add(c);
        }

        return new ResolvedManga(manga, chaps.OrderBy(t => t.Ordinal).ToArray());
    }

    public bool Match(string url) => url.ToLower().StartsWith(HomeUrl);
}
