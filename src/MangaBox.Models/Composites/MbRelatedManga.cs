namespace MangaBox.Models.Composites;

/// <summary>
/// Represents a manga related to another.
/// </summary>
[InterfaceOption(nameof(MbRelatedManga))]
public class MbRelatedManga : IDbModel
{
	/// <summary>
	/// The ID of the manga that is related to the current one.
	/// </summary>
	[Column("manga_id")]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The ID of the group of manga
	/// </summary>
	[Column("work_id")]
	[JsonPropertyName("workId")]
	public Guid WorkId { get; set; }

	/// <summary>
	/// The ID of the source the manga is from
	/// </summary>
	[Column("source_id")]
	[JsonPropertyName("sourceId")]
	public Guid SourceId { get; set; }
}
