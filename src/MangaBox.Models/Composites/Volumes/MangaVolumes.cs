namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Represents a volume of manga
/// </summary>
/// <param name="Progress">The user's progress in the manga</param>
/// <param name="Chapters">The chapters in the manga</param>
/// <param name="Volumes">The volumes of the manga</param>
public record class MangaVolumes(
	[property: JsonPropertyName("progress")] MbMangaProgress? Progress,
	[property: JsonPropertyName("chapters")] Dictionary<Guid, ProgressChapter> Chapters,
	[property: JsonPropertyName("volumes")] MangaVolume[] Volumes);
