namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangaClashSource : IMangaSource { }

public class MangaClashSource : IMangaClashSource 
{
    public string HomeUrl => "https://mangaclash.com/";

    public string MangaBaseUri => $"{HomeUrl}manga / ";

    public string Provider => "mangaclash";

    private readonly IApiService _api;

    public MangaClashSource(IApiService api)
    {
        _api = api;
    }

    public async Task<string[]> Pages(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return Array.Empty<string>();

        return doc.DocumentNode
            .SelectNodes("//div[@class='page-break no-gaps']/img")
            .Select(t => t.GetAttributeValue("data-src", "").Trim('\n', '\t', '\r'))
            .ToArray();
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var doc = await _api.GetHtml(url);
        if (doc == null) return null;

        var manga = new DbManga
        {
            Title = doc.Attribute("//meta[@property='og:title']", "content") ?? "",
            SourceId = SourceHelper.IdFromUrl(url),
            Provider = Provider,
            Url = url,
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

        var chaps = new List<DbMangaChapter>();
        var chapters = doc.DocumentNode.SelectNodes("//li[contains(@class, 'wp-manga-chapter')]/a");
        int i = chapters.Count;
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

    public bool Match(string url) => url.ToLower().StartsWith(HomeUrl);
}
