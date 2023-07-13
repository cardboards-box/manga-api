namespace CardboardBox.Manga.Models.Composites;

[CompositeCodeAttribute]
public class Volume
{
    [JsonPropertyName("name")]
    public double? Name { get; set; }

    [JsonPropertyName("collapse")]
    public bool Collapse { get; set; } = false;

    [JsonPropertyName("read")]
    public bool Read { get; set; } = false;

    [JsonPropertyName("inProgress")]
    public bool InProgress { get; set; } = false;

    [JsonPropertyName("chapters")]
    public List<VolumeChapter> Chapters { get; set; } = new();
}
