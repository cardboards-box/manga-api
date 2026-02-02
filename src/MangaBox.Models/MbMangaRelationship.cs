namespace MangaBox.Models;

using Types;

/// <summary>
/// Represents the relationship between a <see cref="MbManga"/> and a <see cref="MbPerson"/>
/// </summary>
[Table("mb_manga_relationships")]
[BridgeTable<MbManga, MbPerson>(IncludeInChildOrm = false)]
[InterfaceOption(nameof(MbMangaRelationship))]
public class MbMangaRelationship : MbDbObject
{
	/// <summary>
	/// The ID of the manga
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	[Required]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The ID of the person
	/// </summary>
	[Column("person_id", Unique = true), Fk<MbPerson>]
	[JsonPropertyName("personId")]
	[Required]
	public Guid PersonId { get; set; }

	/// <summary>
	/// The type of relationship between the manga and the person
	/// </summary>
	[Column("type", Unique = true)]
	[JsonPropertyName("type")]
	[Required]
	public RelationshipType Type { get; set; }
}
