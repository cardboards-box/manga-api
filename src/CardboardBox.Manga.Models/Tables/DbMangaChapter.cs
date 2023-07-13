namespace CardboardBox.Manga.Models.Tables;

using Types;

[Table("manga_chapter")]
public class DbMangaChapter : DbObject
{
    [JsonPropertyName("mangaId"), Column("manga_id", Unique = true)]
    public long MangaId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sourceId"), Column("source_id", Unique = true)]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("ordinal")]
    public double Ordinal { get; set; }

    [JsonPropertyName("volume")]
    public double? Volume { get; set; }

    [JsonPropertyName("language"), Column(Unique = true)]
    public string Language { get; set; } = "en";

    [JsonPropertyName("pages")]
    public string[] Pages { get; set; } = Array.Empty<string>();

    [JsonPropertyName("externalUrl"), Column("external_url")]
    public string? ExternalUrl { get; set; }

    [JsonPropertyName("attributes")]
    public DbMangaAttribute[] Attributes { get; set; } = Array.Empty<DbMangaAttribute>();
}
