namespace MangaBox.Models.Types;

/// <summary>
/// Tag extension data
/// </summary>
[InterfaceOption(nameof(MbTagExt))]
public class MbTagExt : IDbModel
{
	/// <summary>
	/// The ID of the tag
	/// </summary>
	[Column("tag_id"), JsonPropertyName("id")]
	public Guid TagId { get; set; }

	/// <summary>
	/// Whether or not multiple sources use this tag
	/// </summary>
	[Column("shared"), JsonPropertyName("shared")]
	public bool Shared { get; set; }

	/// <summary>
	/// The content rating shared by all manga using this tag, if any
	/// </summary>
	[Column("restrict_content_rating"), JsonPropertyName("rating")]
	public ContentRating? RestrictContentRating { get; set; }

	/// <summary>
	/// The ID of the source that uniquely uses this tag, if any
	/// </summary>
	[Column("unique_source_id"), JsonPropertyName("source")]
	public Guid? UniqueSourceId { get; set; }

	/// <summary>
	/// The number of manga that use this tag
	/// </summary>
	[Column("manga_count"), JsonPropertyName("manga")]
	public int MangaCount { get; set; }
}
