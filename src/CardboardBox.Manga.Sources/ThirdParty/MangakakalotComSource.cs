namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangakakalotComSource : IMangaSource { }

public class MangakakalotComSource : IMangakakalotComSource
{
    public virtual string HomeUrl => "https://mangakakalot.com/";

    public virtual string MangaBaseUri => $"{HomeUrl}read-";

    public virtual string Provider => "mangakakalot-com";

    private readonly IApiService _api;

    public MangakakalotComSource(IApiService api)
    {
        _api = api;
    }

    public async Task<string[]> Pages(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return Array.Empty<string>();

        return doc
            .DocumentNode
            .SelectNodes("//div[@class='container-chapter-reader']/img")
            .Select(t => t.GetAttributeValue("src", ""))
            .ToArray();
    }

    public virtual async Task<ResolvedManga?> Manga(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return null;

        var title = doc.DocumentNode
            .SelectSingleNode("//ul[@class=\"manga-info-text\"]/li/h1")
            .InnerText;
        var cover = doc.DocumentNode
            .SelectSingleNode("//div[@class=\"manga-info-pic\"]/img")
            .GetAttributeValue("src", "");

        var manga = new DbManga
        {
            Title = title,
            SourceId = url.ToLower().Replace(MangaBaseUri, ""),
            Provider = Provider,
            Url = url,
            Cover = cover,
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
            var href = a.GetAttributeValue("href", "").TrimStart('/');
            if (!href.StartsWith("http")) href = HomeUrl + "/" + href;

            var c = new DbMangaChapter
            {
                Title = a.InnerText.Trim(),
                Url = href,
                Ordinal = num--,
                SourceId = SourceHelper.IdFromUrl(href)
            };

            chaps.Add(c);
        }

        return new ResolvedManga(manga, chaps.OrderBy(t => t.Ordinal).ToArray());
    }

    public bool Match(string url) => MangaBaseUri.ToLower().StartsWith(HomeUrl);
}

public interface IMangakakalotComAltSource : IMangaSource { }

public class MangakakalotComAltSource : MangakakalotComSource, IMangakakalotComAltSource
{
    public override string Provider => "mangakakalot-com-alt";

    public override string MangaBaseUri => $"{HomeUrl}manga/";

    public MangakakalotComAltSource(IApiService api) : base(api) { }
}
