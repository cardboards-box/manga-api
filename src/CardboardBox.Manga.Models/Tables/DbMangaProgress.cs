namespace CardboardBox.Manga.Models.Tables;

using Types;

[Table("manga_progress")]
public class DbMangaProgress : DbObject
{
    [JsonPropertyName("profileId"), Column("profile_id", Unique = true)]
    public long ProfileId { get; set; }

    [JsonPropertyName("mangaId"), Column("manga_id", Unique = true)]
    public long MangaId { get; set; }

    [JsonPropertyName("mangaChapterId"), Column("manga_chapter_id")]
    public long? MangaChapterId { get; set; }

    [JsonPropertyName("pageIndex"), Column("page_index")]
    public int? PageIndex { get; set; }

    [JsonPropertyName("read")]
    public DbMangaChapterProgress[] Read { get; set; } = Array.Empty<DbMangaChapterProgress>();
}
