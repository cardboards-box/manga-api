namespace CardboardBox.Manga.Models.Requests.Filtering;

public class MangaAttributeFilter
{
    [JsonPropertyName("type")]
    public AttributeType Type { get; set; }

    [JsonPropertyName("include")]
    public bool Include { get; set; } = true;

    [JsonPropertyName("values")]
    public string[] Values { get; set; } = Array.Empty<string>();
}
