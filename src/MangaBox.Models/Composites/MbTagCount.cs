namespace MangaBox.Models.Composites;

/// <summary>
/// Represents the number of manga a person has read with the given tag
/// </summary>
public class MbTagCount : IDbModel
{
	/// <summary>
	/// The ID of the tag
	/// </summary>
	[Column("tag_id")]
	public Guid TagId { get; set; }

	/// <summary>
	/// The number of tags
	/// </summary>
	[Column("tag_count")]
	public int TagCount { get; set; }
}
