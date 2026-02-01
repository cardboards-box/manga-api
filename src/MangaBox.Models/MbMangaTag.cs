namespace MangaBox.Models;

/// <summary>
/// The bridge between <see cref="MbManga"/>s and <see cref="MbTag"/>s
/// </summary>
[Table("mb_manga_tags")]
[BridgeTable<MbManga, MbTag>(IncludeInChildOrm = false)]
[InterfaceOption(nameof(MbMangaTag))]
public class MbMangaTag : MbDbObject
{
	/// <summary>
	/// The ID of the manga
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	[Required]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The ID of the tag
	/// </summary>
	[Column("tag_id", Unique = true), Fk<MbTag>]
	[JsonPropertyName("tagId")]
	[Required]
	public Guid TagId { get; set; }
}
