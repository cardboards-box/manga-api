namespace CardboardBox.Manga.ReverseImageSearch.MatchApi;

public class MangaMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public MangaMetadataType Type { get; set; }

    [JsonPropertyName("mangaId")]
    public string MangaId { get; set; } = string.Empty;

    [JsonPropertyName("chapterId")]
    public string? ChapterId { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }
}