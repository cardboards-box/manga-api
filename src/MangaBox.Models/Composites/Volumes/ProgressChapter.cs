namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// Represents a chapter within a manga
/// </summary>
/// <param name="Chapter">The chapter information</param>
/// <param name="Progress">The user's progress in the chapter</param>
public record class ProgressChapter(
	[property: JsonPropertyName("chapter")] MbChapter Chapter,
	[property: JsonPropertyName("progress")] MbChapterProgress? Progress);
