namespace CardboardBox.Manga.Models.Tables;

[Table("manga_bookmarks")]
public class DbMangaBookmark : DbObject
{
    [JsonPropertyName("profileId"), Column("profile_id", Unique = true)]
    public long ProfileId { get; set; }

    [JsonPropertyName("mangaId"), Column("manga_id", Unique = true)]
    public long MangaId { get; set; }

    [JsonPropertyName("mangaChapterId"), Column("manga_chapter_id", Unique = true)]
    public long MangaChapterId { get; set; }

    [JsonPropertyName("pages")]
    public int[] Pages { get; set; } = Array.Empty<int>();
}
