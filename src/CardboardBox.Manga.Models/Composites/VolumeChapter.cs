namespace CardboardBox.Manga.Models;

[CompositeCode]
public class VolumeChapter
{
    [JsonPropertyName("read")]
    public bool Read { get; set; } = false;

    [JsonPropertyName("readIndex")]
    public int? ReadIndex { get; set; }

    [JsonPropertyName("pageIndex")]
    public int? PageIndex { get; set; }

    [JsonPropertyName("versions")]
    public DbMangaChapter[] Versions { get; set; } = Array.Empty<DbMangaChapter>();

    [JsonPropertyName("open")]
    public bool Open { get; set; } = false;

    [JsonPropertyName("progress")]
    public double? Progress { get; set; } = null;
}
