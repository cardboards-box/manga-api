namespace CardboardBox.Manga.Models.Composites;

using Tables;

[CompositeTuple]
public class MangaData : MangaWithChapters
{
    [JsonIgnore]
    public override DbMangaChapter[] Chapters { get; set; } = Array.Empty<DbMangaChapter>();

    [JsonPropertyName("chapter")]
    public DbMangaChapter Chapter { get; set; } = new();

    [JsonPropertyName("volumes")]
    public Volume[] Volumes { get; set; } = Array.Empty<Volume>();

    [JsonPropertyName("progress")]
    public DbMangaProgress? Progress { get; set; }

    [JsonPropertyName("stats")]
    public MangaStats? Stats { get; set; }

    [JsonPropertyName("volumeIndex")]
    public int VolumeIndex { get; set; }
}
