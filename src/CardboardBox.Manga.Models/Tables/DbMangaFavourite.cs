namespace CardboardBox.Manga.Models;

[Table("manga_favourites")]
public class DbMangaFavourite : DbObject
{
    [JsonPropertyName("profileId"), Column("profile_id", Unique = true)]
    public long ProfileId { get; set; }

    [JsonPropertyName("mangaId"), Column("manga_id", Unique = true)]
    public long MangaId { get; set; }
}
