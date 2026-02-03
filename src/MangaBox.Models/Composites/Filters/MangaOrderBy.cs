namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The order criteria for manga searches
/// </summary>
public enum MangaOrderBy
{
	/// <summary>
	/// Order by when the manga was created
	/// </summary>
	[Display(Name = "Manga Created At")]
	[Description("Order by when the manga was created")]
	MangaCreatedAt,
	/// <summary>
	/// Order by when the manga was last updated
	/// </summary>
	[Display(Name = "Manga Updated At")]
	[Description("Order by when the manga was last updated")]
	MangaUpdatedAt,
	/// <summary>
	/// When the most recent chapter was created
	/// </summary>
	[Display(Name = "Last Chapter Created At")]
	[Description("When the most recent chapter was created")]
	LastChapterCreatedAt,
	/// <summary>
	/// When the most recent chapter was updated
	/// </summary>
	[Display(Name = "Last Chapter Updated At")]
	[Description("When the most recent chapter was updated")]
	LastChapterUpdatedAt,
	/// <summary>
	/// When the oldest chapter was created
	/// </summary>
	[Display(Name = "First Chapter Created At")]
	[Description("When the oldest chapter was created")]
	FirstChapterCreatedAt,
	/// <summary>
	/// When the oldest chapter was updated
	/// </summary>
	[Display(Name = "First Chapter Updated At")]
	[Description("When the oldest chapter was updated")]
	FirstChapterUpdatedAt,
	/// <summary>
	/// The total number of unique chapters in the manga
	/// </summary>
	[Display(Name = "Chapter Count")]
	[Description("The total number of unique chapters in the manga")]
	ChapterCount,
	/// <summary>
	/// The total number of volumes in the manga
	/// </summary>
	[Display(Name = "Volume Count")]
	[Description("The total number of volumes in the manga")]
	VolumeCount,
	/// <summary>
	/// The total number of views in the manga
	/// </summary>
	[Display(Name = "Views")]
	[Description("The total number of views in the manga")]
	Views,
	/// <summary>
	/// The total number of people who have favorited the manga
	/// </summary>
	[Display(Name = "Favorites")]
	[Description("The total number of people who have favorited the manga")]
	Favorites,
	/// <summary>
	/// The title of the manga
	/// </summary>
	[Display(Name = "Title")]
	[Description("The title of the manga")]
	Title,
	/// <summary>
	/// The last read time of the manga
	/// </summary>
	[Display(Name = "Last Read")]
	[Description("The last read time of the manga")]
	LastRead,
	/// <summary>
	/// Orders the manga randomly
	/// </summary>
	[Display(Name = "Random")]
	[Description("Orders the manga randomly")]
	Random,
}
