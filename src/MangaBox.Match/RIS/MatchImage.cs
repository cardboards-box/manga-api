namespace MangaBox.Match.RIS;

/// <summary>
/// Represents an image that has been matched with a similarity score
/// </summary>
public class MatchImage : MatchScore
{
	/// <summary>
	/// The file path to the matching image
	/// </summary>
	[JsonPropertyName("filepath")]
	public string FilePath { get; set; } = string.Empty;
}
