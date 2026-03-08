namespace MangaBox.Models.Composites;

/// <summary>
/// Represents a cover image associated with a specific list.
/// </summary>
public class MbListCoverImage : MbImage
{
	/// <summary>
	/// The ID of the list the cover is associated with
	/// </summary>
	[Column("list_id"), Fk<MbList>]
	[JsonIgnore]
	public Guid ListId { get; set; }
}
