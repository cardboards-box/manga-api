namespace CardboardBox.Manga.Models.Composites;

[Composite]
public class GraphOut
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
