namespace MangaBox.Match.SauceNao;

/// <summary>
/// The meta-data about the sauce result
/// </summary>
public class SauceMetaData
{
	/// <summary>
	/// The similarity rating between the images
	/// </summary>
	[JsonPropertyName("similarity")]
	public string Similarity { get; set; } = string.Empty;

	/// <summary>
	/// The thumbnail of the image returned
	/// </summary>
	[JsonPropertyName("thumbnail")]
	public string Thumbnail { get; set; } = string.Empty;

	/// <summary>
	/// The ID of the sauce-index/database being used
	/// </summary>
	[JsonPropertyName("index_id")]
	public int IndexId { get; set; }

	/// <summary>
	/// The name of the sauce-index/database being used
	/// </summary>
	[JsonPropertyName("index_name")]
	public string IndexName { get; set; } = string.Empty;
}
