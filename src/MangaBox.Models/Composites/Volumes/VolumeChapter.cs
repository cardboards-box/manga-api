namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Represents a volume within a chapter
/// </summary>
/// <param name="Progress">The progress percentage of the chapter</param>
/// <param name="Ordinal">The ordinal of the chapter</param>
/// <param name="Versions">The various versions of the chapters</param>
public record class VolumeChapter(
	[property: JsonPropertyName("progress")] double Progress,
	[property: JsonPropertyName("ordinal")] double Ordinal,
	[property: JsonPropertyName("versions")] Guid[] Versions);
