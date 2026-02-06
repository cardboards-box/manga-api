namespace MangaBox.Match.RIS;

/// <summary>
/// The metadata for a manga image from an internal source
/// </summary>
[InterfaceOption(MatchSearchService.SERVICE_SLUG)]
public class MangaMetadata : IImageSearchResult
{
	/// <summary>
	/// The ID of the image
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// The URL of the image
	/// </summary>
	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	/// <summary>
	/// The source slug
	/// </summary>
	[JsonPropertyName("source")]
	public string Source { get; set; } = string.Empty;

	/// <summary>
	/// The type of page
	/// </summary>
	[JsonPropertyName("type")]
	public MangaMetadataType Type { get; set; }

	/// <summary>
	/// The source-specific ID of the manga
	/// </summary>
	[JsonPropertyName("mangaId")]
	public string MangaId { get; set; } = string.Empty;

	/// <summary>
	/// The source-specific ID of the chapter
	/// </summary>
	[JsonPropertyName("chapterId")]
	public string? ChapterId { get; set; }

	/// <summary>
	/// The page index
	/// </summary>
	[JsonPropertyName("page")]
	public int? Page { get; set; }
}

/// <summary>
/// The type of meta-data image
/// </summary>
public enum MangaMetadataType
{
	/// <summary>
	/// A page of a chapter
	/// </summary>
	Page,
	/// <summary>
	/// A cover of a manga
	/// </summary>
	Cover
}
