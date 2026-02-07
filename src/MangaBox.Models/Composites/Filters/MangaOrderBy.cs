namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The order criteria for manga searches
/// </summary>
public enum MangaOrderBy
{
	/// <summary>
	/// When the most recent chapter was created
	/// </summary>
	[Display(Name = "Last Chapter Created At")]
	[Description("When the most recent chapter was created")]
	LastChapterCreatedAt = 0,
	/// <summary>
	/// When the most recent chapter was updated
	/// </summary>
	[Display(Name = "Last Chapter Updated At")]
	[Description("When the most recent chapter was updated")]
	LastChapterUpdatedAt = 1,
	/// <summary>
	/// When the oldest chapter was created
	/// </summary>
	[Display(Name = "First Chapter Created At")]
	[Description("When the oldest chapter was created")]
	FirstChapterCreatedAt = 2,
	/// <summary>
	/// When the oldest chapter was updated
	/// </summary>
	[Display(Name = "First Chapter Updated At")]
	[Description("When the oldest chapter was updated")]
	FirstChapterUpdatedAt = 3,
	/// <summary>
	/// Order by when the manga was created
	/// </summary>
	[Display(Name = "Manga Created At")]
	[Description("Order by when the manga was created")]
	MangaCreatedAt = 4,
	/// <summary>
	/// Order by when the manga was last updated
	/// </summary>
	[Display(Name = "Manga Updated At")]
	[Description("Order by when the manga was last updated")]
	MangaUpdatedAt = 5,
	/// <summary>
	/// The total number of unique chapters in the manga
	/// </summary>
	[Display(Name = "Chapter Count")]
	[Description("The total number of unique chapters in the manga")]
	ChapterCount = 6,
	/// <summary>
	/// The total number of volumes in the manga
	/// </summary>
	[Display(Name = "Volume Count")]
	[Description("The total number of volumes in the manga")]
	VolumeCount = 7,
	/// <summary>
	/// The total number of views in the manga
	/// </summary>
	[Display(Name = "Views")]
	[Description("The total number of views in the manga")]
	Views = 8,
	/// <summary>
	/// The total number of people who have favorited the manga
	/// </summary>
	[Display(Name = "Favorites")]
	[Description("The total number of people who have favorited the manga")]
	Favorites = 9,
	/// <summary>
	/// The title of the manga
	/// </summary>
	[Display(Name = "Title")]
	[Description("The title of the manga")]
	Title = 10,
	/// <summary>
	/// The last read time of the manga
	/// </summary>
	[Display(Name = "Last Read")]
	[Description("The last read time of the manga")]
	LastRead = 11,
	/// <summary>
	/// Orders the manga randomly
	/// </summary>
	[Display(Name = "Random")]
	[Description("Orders the manga randomly")]
	Random = 12,
}
