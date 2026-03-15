namespace MangaBox.Models;

/// <summary>
/// Represents extended information about a manga list
/// </summary>
[Table("mb_list_ext")]
[InterfaceOption(nameof(MbListExt))]
public class MbListExt : MbDbObject
{
	/// <summary>
	/// The ID of the list this extension belongs to
	/// </summary>
	[Column("list_id", Unique = true), Fk<MbList>]
	[JsonPropertyName("listId")]
	public Guid ListId { get; set; }

	/// <summary>
	/// The ID of the cover image to use for the list
	/// </summary>
	[Column("cover_id"), Fk<MbImage>]
	[JsonPropertyName("coverId")]
	public Guid? CoverId { get; set; }

	/// <summary>
	/// The number of manga currently in the list
	/// </summary>
	[Column("manga_count")]
	[JsonPropertyName("mangaCount")]
	public int MangaCount { get; set; }

	/// <summary>
	/// The number of times this list has been cloned
	/// </summary>
	[Column("cloned_count")]
	[JsonPropertyName("clonedCount")]
	public int ClonedCount { get; set; }
}
