using MangaDexSharp;

namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangaDexSource : IMangaSource { }

public class MangaDexSource : IMangaDexSource
{
    public string HomeUrl => "https://mangadex.org";
    public string Provider => "mangadex";

    private readonly IMangaDex _mangadex;

    public MangaDexSource(IMangaDex mangadex)
    {
        _mangadex = mangadex;
    }

    public async Task<string[]> Pages(string url)
    {
        var id = SourceHelper.IdFromUrl(url);
        var pages = await _mangadex.Pages.Pages(id);
        if (pages == null)
            return Array.Empty<string>();

        return pages.Images;
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var id = IdFromUrl(url);
        var data = await _mangadex.Manga.Get(id, new[] 
        { 
            MangaIncludes.cover_art, 
            MangaIncludes.author, 
            MangaIncludes.artist, 
            MangaIncludes.scanlation_group, 
            MangaIncludes.tag, 
            MangaIncludes.chapter 
        });

        if (data == null || data.Data == null) return null;
        
        var manga = data.Data;

        var chapters = await GetChapters(id, MangaExtensions.DEFAULT_LANG)
            .OrderBy(t => t.Ordinal)
            .ToArrayAsync();

        var output = MangaExtensions.Convert(manga);
        return new ResolvedManga(output, chapters);
    }

    public bool Match(string url) => url.StartsWith(HomeUrl);

    public async IAsyncEnumerable<DbMangaChapter> GetChapters(string id, params string[] languages)
    {
        var filter = new MangaFeedFilter { TranslatedLanguage = languages };
        while (true)
        {
            var chapters = await _mangadex.Manga.Feed(id, filter);
            if (chapters == null) yield break;

            var sortedChapters = chapters
                .Data
                .GroupBy(t => t.Attributes.Chapter)
                .Select(t => t.PreferedOrFirst(t => t.Attributes.TranslatedLanguage == MangaExtensions.DEFAULT_LANG))
                .Where(t => t != null)
                .Select(t => MangaExtensions.Convert(t!))
                .OrderBy(t => t.Ordinal);

            foreach (var chap in sortedChapters)
                yield return chap;

            int current = chapters.Offset + chapters.Limit;
            if (chapters.Total <= current) yield break;

            filter.Offset = current;
        }
    }

    public static string IdFromUrl(string url)
    {
        var parts = url.Replace("https://mangadex.org/", "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts.Last() : parts.Skip(1).First();
    }
}
