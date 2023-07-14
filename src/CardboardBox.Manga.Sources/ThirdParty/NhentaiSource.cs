namespace CardboardBox.Manga.Sources;

using Models;

public interface INhentaiSource : IMangaSource { }

public class NHentaiSource : INhentaiSource
{
    private const string DEFAULT_CHAPTER_TITLE = "Chapter 1";

    public string HomeUrl => "https://nhentai.to/";
    public string MangaBaseUri => $"{HomeUrl}g/";
    public string Provider => "nhentai";

    private readonly IApiService _api;

    public NHentaiSource(IApiService api)
    {
        _api = api;
    }

    public static string FixPreview(string url)
    {
        var parts = url.Split('/');
        var fname = parts.Last();

        var ext = Path.GetExtension(fname);
        var fwext = Path.GetFileNameWithoutExtension(fname);
        if (fwext.EndsWith("t"))
            fwext = fwext[..^1];

        return string.Join('/', parts.SkipLast().Append($"{fwext}{ext}"));
    }

    public async Task<string[]> Pages(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return Array.Empty<string>();

        return Pages(doc);
    }

    public static string[] Pages(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//div[@class='container']/div[@class='thumb-container']/a/img")
            .Select(t => FixPreview(t.GetAttributeValue("data-src", "").Trim()))
            .ToArray();
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return null;

        var manga = new DbManga
        {
            Title = doc.InnerText("//div[@id='info']/h1")?.Trim() ?? "",
            SourceId = url.Split('/').Last(),
            Referer = HomeUrl,
            Provider = Provider,
            Url = url,
            Cover = doc.Attribute("//div[@id='cover']/a/img", "src") ?? "",
            Nsfw = true,
            Tags = doc.DocumentNode
                      .SelectNodes("//span[@class='tags']/a[contains(@href, '/tag')]/span[@class='name']")
                      .Select(t => t.InnerText.Trim())
                      .ToArray()
        };

        var chapters = new[]
        {
            new DbMangaChapter
            {
                SourceId = manga.SourceId,
                Title = DEFAULT_CHAPTER_TITLE,
                Url = url,
                Pages = Pages(doc),
                Ordinal = 1,
                Volume = 1,
            }
        };

        return new ResolvedManga(manga, chapters);
    }

    public bool Match(string url) => url.ToLower().StartsWith(HomeUrl);
}
