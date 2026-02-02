namespace MangaBox.Models.Composites;

/// <summary>
/// The results of an upsert request
/// </summary>
public class UpsertResult
{
	/// <summary>
	/// The manga that was upserted
	/// </summary>
	[JsonPropertyName("manga")]
	public MbManga Manga { get; set; } = new();

	/// <summary>
	/// All of the chapters that were updated
	/// </summary>
	[JsonPropertyName("chaptersUpdated")]
	public MbChapter[] ChaptersUpdated { get; set; } = [];

	/// <summary>
	/// All of the chapters that are new
	/// </summary>
	[JsonPropertyName("chaptersNew")]
	public MbChapter[] ChaptersNew { get; set; } = [];

	/// <summary>
	/// Whether or not the manga is new or has been updated
	/// </summary>
	[JsonPropertyName("mangaIsNew")]
	public bool MangaIsNew { get; set; }
}
