namespace CardboardBox.Manga.Models;

[Table("manga")]
public class DbManga : DbObject
{
    [JsonPropertyName("hashId"), Column("hash_id")]
    public string HashId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("sourceId"), Column("source_id", Unique = true)]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("provider"), Column(Unique = true)]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("cover")]
    public string Cover { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("altTitles"), Column("alt_titles")]
    public string[] AltTitles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; } = false;

    [JsonPropertyName("attributes")]
    public DbMangaAttribute[] Attributes { get; set; } = Array.Empty<DbMangaAttribute>();

    [JsonPropertyName("referer")]
    public string? Referer { get; set; }

    [JsonPropertyName("sourceCreated"), Column("source_created")]
    public DateTime? SourceCreated { get; set; }
}
