namespace CardboardBox.Manga.Sources;

using Models;

public interface IMangaImportService
{
    IMangaSource[] Sources { get; }

    IMangaSource? SourceFromUrl(string url);

    Task<ResolvedManga?> Manga(string url);

    Task<string[]> Pages(DbManga manga, string url);
}

public partial class MangaImportService : IMangaImportService
{
    private readonly IMangaSource[] _sources;

    public IMangaSource[] Sources => _sources;

    public MangaImportService(
        IMangaClashSource mangaClash,
        IMangaDexSource mangaDex,
        IMangakakalotComSource mangakakalotCom,
        IMangakakalotComAltSource mangakakalotComAlt,
        IMangakakalotTvSource mangakakalotTv,
        IMangaKatanaSource mangaKatana,
        INhentaiSource nhentai)
    {
        _sources = new[]
        {
            (IMangaSource)mangaClash,
            mangaDex,
            mangakakalotCom,
            mangakakalotComAlt,
            mangakakalotTv,
            mangaKatana,
            nhentai
        };
    }

    public IMangaSource? SourceFromUrl(string url)
    {
        return _sources.FirstOrDefault(t => t.Match(url));
    }

    public async Task<ResolvedManga?> Manga(string url)
    {
        var source = SourceFromUrl(url);
        if (source == null) return null;

        var resolved = await source.Manga(url);
        if (resolved == null) return null;

        resolved.Manga.HashId = resolved.Manga.GetHashId();
        return resolved;
    }

    public async Task<string[]> Pages(DbManga manga, string url)
    {
        var source = SourceFromUrl(manga.Url);
        if (source == null) return Array.Empty<string>();

        return await source.Pages(url);
    }
}
