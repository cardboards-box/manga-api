namespace MangaBox.Models;

/// <summary>
/// Represents all of the items in a <see cref="MbList"/>
/// </summary>
[Table("mb_list_items")]
[InterfaceOption(nameof(MbListItem))]
[BridgeTable<MbList, MbManga>(IncludeInChildOrm = false, IncludeInParentOrm = true)]
public class MbListItem : MbDbObject
{
	/// <summary>
	/// The ID of the list this item belongs to
	/// </summary>
	[Column("list_id", Unique = true), Fk<MbList>]
	[JsonPropertyName("listId")]
	public Guid ListId { get; set; }

	/// <summary>
	/// The ID of the manga this item belongs to
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// A request to create a new list item
	/// </summary>
	/// <param name="ListId">The ID of the list</param>
	/// <param name="MangaId">The ID of the manga</param>
	/// <param name="ProfileId">The ID of the profile</param>
	public record class LinkRequest(
		[property: JsonPropertyName("listId")] Guid ListId,
		[property: JsonPropertyName("mangaId")] Guid MangaId,
		[property: JsonPropertyName("profileId")] Guid? ProfileId);
}
