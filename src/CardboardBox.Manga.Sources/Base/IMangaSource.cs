namespace CardboardBox.Manga.Sources;

public interface IMangaSource
{
    string HomeUrl { get; }
    string Provider { get; }

    Task<ResolvedManga?> Manga(string url);

    Task<string[]> Pages(string url);

    bool Match(string url);
}