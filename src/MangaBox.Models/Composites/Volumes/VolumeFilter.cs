namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// The volume filter
/// </summary>
public class VolumeFilter
{
	/// <summary>
	/// The ID of the manga
	/// </summary>
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// How to order the chapters
	/// </summary>
	[JsonPropertyName("order")]
	public ChapterOrderBy Order { get; set; } = ChapterOrderBy.Ordinal;

	/// <summary>
	/// The direction of the chapter order
	/// </summary>
	[JsonPropertyName("asc")]
	public bool Asc { get; set; } = true;
}
