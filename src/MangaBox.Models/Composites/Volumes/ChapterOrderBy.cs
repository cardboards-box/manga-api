namespace MangaBox.Models.Composites.Volumes;

/// <summary>
/// How to order chapters when volume-ing
/// </summary>
public enum ChapterOrderBy
{
	/// <summary>
	/// The ordinal of the chapter
	/// </summary>
	[Display(Name = "Chapter Ordinal")]
	[Description("The ordinal of the chapter")]
	Ordinal = 0,
	/// <summary>
	/// The date the chapter was released
	/// </summary>
	[Display(Name = "Release Date")]
	[Description("The date the chapter was released")]
	Date = 1,
	/// <summary>
	/// The language of the chapter
	/// </summary>
	[Display(Name = "Language")]
	[Description("The language of the chapter")]
	Language = 2,
	/// <summary>
	/// The title of the chapter
	/// </summary>
	[Display(Name = "Title")]
	[Description("The chapter's title")]
	Title = 3,
	/// <summary>
	/// Whether or not the chapter has been read
	/// </summary>
	[Display(Name = "Read Status")]
	[Description("Whether or not you have read the chapter")]
	Read = 4
}
