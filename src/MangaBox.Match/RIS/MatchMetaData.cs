namespace MangaBox.Match.RIS;

/// <summary>
/// Represents an image that has been matched with a similarity score with metadata
/// </summary>
/// <typeparam name="T">The type of meta-data</typeparam>
public class MatchMetaData<T> : MatchImage
{
	/// <summary>
	/// The meta-data associated with the matched image
	/// </summary>
	[JsonPropertyName("metadata")]
	public T? MetaData { get; set; }
}
