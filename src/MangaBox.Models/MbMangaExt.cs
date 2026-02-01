namespace MangaBox.Models;

/// <summary>
/// Represents extended information about a manga
/// </summary>
[Table("mb_manga_ext")]
[InterfaceOption(nameof(MbMangaExt))]
public class MbMangaExt : MbDbObject
{
	/// <summary>
	/// The ID of the manga this extension belongs to
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The total number of chapters in the manga
	/// </summary>
	/// <remarks>The is the de-duped number of chapters, not the total</remarks>
	[Column("chapter_count")]
	[JsonPropertyName("chapterCount")]
	public int ChapterCount { get; set; }

	/// <summary>
	/// The total number of chapters in the manga
	/// </summary>
	/// <remarks>This includes duplicate chapter numbers</remarks>
	[Column("unique_chapter_count")]
	[JsonPropertyName("uniqueChapterCount")]
	public int UniqueChapterCount { get; set; }

	/// <summary>
	/// The last chapter ordinal number in the manga
	/// </summary>
	[Column("last_chapter_ordinal")]
	[JsonPropertyName("lastChapterOrdinal")]
	public double LastChapterOrdinal { get; set; }

	/// <summary>
	/// The first chapter ordinal number in the manga
	/// </summary>
	[Column("first_chapter_ordinal")]
	[JsonPropertyName("firstChapterOrdinal")]
	public double FirstChapterOrdinal { get; set; }

	/// <summary>
	/// The date/time the latest chapter was uploaded
	/// </summary>
	[Column("last_chapter_created")]
	[JsonPropertyName("lastChapterCreated")]
	public DateTime LastChapterCreated { get; set; }

	/// <summary>
	/// The date/time the first chapter was uploaded
	/// </summary>
	[Column("first_chapter_created")]
	[JsonPropertyName("firstChapterCreated")]
	public DateTime FirstChapterCreated { get; set; }

	/// <summary>
	/// The ID of the last chapter released
	/// </summary>
	[Column("last_chapter_id"), Fk<MbChapter>]
	[JsonPropertyName("lastChapterId")]
	public Guid? LastChapterId { get; set; }

	/// <summary>
	/// The ID of the first chapter released
	/// </summary>
	[Column("first_chapter_id"), Fk<MbChapter>]
	[JsonPropertyName("firstChapterId")]
	public Guid? FirstChapterId { get; set; }

	/// <summary>
	/// The total number of volumes in the manga
	/// </summary>
	[Column("volume_count")]
	[JsonPropertyName("volumeCount")]
	public int VolumeCount { get; set; }

	/// <summary>
	/// The average number of days between uploads to the manga
	/// </summary>
	[Column("days_between_updates")]
	[JsonPropertyName("daysBetweenUpdates")]
	public double DaysBetweenUpdates { get; set; }

	/// <summary>
	/// The total number of people who have viewed this manga on MB
	/// </summary>
	[Column("views")]
	[JsonPropertyName("views")]
	public int Views { get; set; }

	/// <summary>
	/// The total number of people who have favourited this manga on MB
	/// </summary>
	[Column("favorites")]
	[JsonPropertyName("favorites")]
	public int Favorites { get; set; }

	/// <summary>
	/// The override display title for the manga
	/// </summary>
	[Column("display_title")]
	[JsonPropertyName("displayTitle")]
	public string? DisplayTitle { get; set; }
}
