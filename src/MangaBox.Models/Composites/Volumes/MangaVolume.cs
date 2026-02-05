namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// The various volumes in the manga
/// </summary>
/// <param name="Ordinal">The ordinal of the volume in the manga</param>
/// <param name="State">The state of the volume</param>
/// <param name="Chapters">The chapters in the volume</param>
public record class MangaVolume(
	[property: JsonPropertyName("ordinal")] double? Ordinal,
	[property: JsonPropertyName("state")] VolumeState State,
	[property: JsonPropertyName("chapters")] VolumeChapter[] Chapters);
