namespace CardboardBox.Manga.Models;

[CompositeTuple]
public class MangaWithChapters
{
    [JsonPropertyName("manga")]
    public DbManga Manga { get; set; } = new();

    [JsonPropertyName("chapters")]
    public virtual DbMangaChapter[] Chapters { get; set; } = Array.Empty<DbMangaChapter>();

    [JsonPropertyName("bookmarks")]
    public DbMangaBookmark[] Bookmarks { get; set; } = Array.Empty<DbMangaBookmark>();

    [JsonPropertyName("favourite")]
    public bool Favourite { get; set; } = false;

    public MangaWithChapters() { }

    public MangaWithChapters(
        DbManga manga, 
        DbMangaChapter[] chapters)
    {
        Manga = manga;
        Chapters = chapters;
    }

    public MangaWithChapters(
        DbManga manga, 
        DbMangaChapter[] chapters, 
        DbMangaBookmark[] bookmarks, 
        bool favourite) : this(manga, chapters)
    {
        Bookmarks = bookmarks;
        Favourite = favourite;
    }

    public void Deconstruct(out DbManga manga, out DbMangaChapter[] chapters)
    {
        manga = Manga;
        chapters = Chapters;
    }
}
