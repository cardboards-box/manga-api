namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Represents a set of chapters that share the same primary ordinal
/// </summary>
/// <param name="Ordinal">The ordinal for the chapter</param>
/// <param name="Versions">An array of chapter version IDs</param>
public record class VolumeChapterPartial(
	[property: JsonPropertyName("ordinal")] double Ordinal,
	[property: JsonPropertyName("versions")] Guid[] Versions);
