namespace CardboardBox.Manga.Models.Composites;

using Tables;

[CompositeTuple]
public class MangaExtended
{
    [JsonPropertyName("manga")]
    public DbManga Manga { get; set; }

    [JsonPropertyName("progress")]
    public DbMangaProgress? Progress { get; set; }

    [JsonPropertyName("chapter")]
    public DbMangaChapter Chapter { get; set; }

    [JsonPropertyName("stats")]
    public MangaStats Stats { get; set; }

    public MangaExtended(
        DbManga manga,
        DbMangaProgress progress,
        DbMangaChapter chapter,
        MangaStats stats)
    {
        Manga = manga;
        Progress = progress?.Id == 0 ? null : progress;
        Chapter = chapter;
        Stats = stats;
    }
}