namespace MangaBox.Match.RIS;

/// <summary>
/// The comparison score between two images
/// </summary>
public class MatchScore
{
	/// <summary>
	/// The similarity score between the two images
	/// </summary>
	[JsonPropertyName("score")]
	public float Score { get; set; }
}
