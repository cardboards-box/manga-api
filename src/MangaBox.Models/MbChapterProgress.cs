namespace MangaBox.Models;

/// <summary>
/// Represents the progess of a profile in a chapter
/// </summary>
[Table("mb_chapter_progress")]
[InterfaceOption(nameof(MbChapterProgress))]
public class MbChapterProgress : MbDbObject
{
	/// <summary>
	/// The ID of the manga progress entity this belongs to
	/// </summary>
	[Column("progress_id", Unique = true), Fk<MbMangaProgress>]
	[JsonPropertyName("progressId")]
	public Guid ProgressId { get; set; }

	/// <summary>
	/// The ID of the chapter this progress belongs to
	/// </summary>
	[Column("chapter_id", Unique = true), Fk<MbChapter>(ignore: true)]
	[JsonPropertyName("chapterId")]
	public Guid ChapterId { get; set; }

	/// <summary>
	/// The ordinal of the page the user is currently on
	/// </summary>
	[Column("page_ordinal")]
	[JsonPropertyName("pageOrdinal")]
	public int? PageOrdinal { get; set; }

	/// <summary>
	/// The page indexes that have been bookmarked in this chapter
	/// </summary>
	[Column("bookmarks")]
	[JsonPropertyName("bookmarks")]
	public int[] Bookmarks { get; set; } = [];

	/// <summary>
	/// The last time the user read the chapter
	/// </summary>
	[Column("last_read")]
	[JsonPropertyName("lastRead")]
	public DateTime? LastRead { get; set; }
}
