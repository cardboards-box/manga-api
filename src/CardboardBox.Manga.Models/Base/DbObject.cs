namespace CardboardBox.Manga.Models;

public abstract class DbObject
{
    [JsonPropertyName("id")]
    [Column(PrimaryKey = true, ExcludeInserts = true, ExcludeUpdates = true)]
    public long Id { get; set; }

    [JsonPropertyName("createdAt")]
    [Column("created_at", ExcludeInserts = true, ExcludeUpdates = true, OverrideValue = "CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    [Column("updated_at", ExcludeInserts = true, OverrideValue = "CURRENT_TIMESTAMP")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }
}