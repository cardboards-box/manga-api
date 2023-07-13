namespace CardboardBox.Manga.Models.Composites;

[Composite]
public class DbFilter
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
