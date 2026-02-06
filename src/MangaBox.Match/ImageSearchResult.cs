namespace MangaBox.Match;

/// <summary>
/// Represents a result from an image search service
/// </summary>
[Interface(typeof(IImageSearchResult), nameof(Source), nameof(Result))]
[JsonConverter(typeof(InterfaceParser<ImageSearchResult>))]
public class ImageSearchResult
{
	/// <summary>
	/// The image search service that found this result
	/// </summary>
	[JsonPropertyName("source")]
	public string Source { get; set; } = string.Empty;

	/// <summary>
	/// The image that matched
	/// </summary>
	[JsonPropertyName("image")]
	public string? Image { get; set; }

	/// <summary>
	/// The score of the result
	/// </summary>
	[JsonPropertyName("score")]
	public double Score { get; set; }

	/// <summary>
	/// Whether or not the result is an exact match
	/// </summary>
	[JsonPropertyName("exact")]
	public bool Exact { get; set; }

	/// <summary>
	/// The closest match in the manga-box database, if any
	/// </summary>
	[JsonPropertyName("closest")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public MangaBoxType<MbManga>? Closest { get; set; }

	/// <summary>
	/// The payload from the source
	/// </summary>
	[JsonPropertyName("result")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IImageSearchResult? Result { get; set; }
}

/// <summary>
/// The base object for the search results
/// </summary>
public interface IImageSearchResult { }