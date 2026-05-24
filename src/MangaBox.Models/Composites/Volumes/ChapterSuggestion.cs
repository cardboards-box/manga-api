namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// The suggestion for the next chapter to read.
/// </summary>
/// <param name="Id">The ID of the chapter to read next</param>
/// <param name="Transition">The type of transition to the next chapter</param>
public record class ChapterSuggestion(
	[property: JsonPropertyName("id")] Guid? Id,
	[property: JsonPropertyName("transition")] TransitionType Transition);