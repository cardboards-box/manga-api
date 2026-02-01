namespace MangaBox.Models;

/// <summary>
/// The bridge between manga and profiles, indicating a user's progress through the manga
/// </summary>
[Table("mb_manga_progress")]
[InterfaceOption(nameof(MbMangaProgress))]
[BridgeTable<MbProfile, MbManga>(IncludeInChildOrm = false)]
public class MbMangaProgress : MbDbObject
{
	/// <summary>
	/// The ID of the profile this progress belongs to
	/// </summary>
	[Column("profile_id", Unique = true), Fk<MbProfile>]
	[JsonPropertyName("profileId")]
	public Guid ProfileId { get; set; }

	/// <summary>
	/// The ID of the manga this progress belongs to
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The last read chapter ordinal number
	/// </summary>
	[Column("last_read_ordinal")]
	[JsonPropertyName("lastReadOrdinal")]
	public double? LastReadOrdinal { get; set; }

	/// <summary>
	/// The ID of the last chapter read
	/// </summary>
	[Column("last_read_chapter_id"), Fk<MbChapter>]
	[JsonPropertyName("lastReadChapterId")]
	public Guid? LastReadChapterId { get; set; }

	/// <summary>
	/// Whether the manga is marked as completed
	/// </summary>
	[Column("is_completed")]
	[JsonPropertyName("isCompleted")]
	public bool IsCompleted { get; set; }

	/// <summary>
	/// Whether or not the user has favorited the manga
	/// </summary>
	[Column("favorited")]
	[JsonPropertyName("favorited")]
	public bool Favorited { get; set; }
}
