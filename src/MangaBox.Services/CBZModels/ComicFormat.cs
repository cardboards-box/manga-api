namespace MangaBox.Services.CBZModels;

/// <summary>
/// The formats for comic archives.
/// </summary>
public enum ComicFormat
{
	/// <summary>
	/// A regular ZIP archive
	/// </summary>
	[Display(Name = "Zip Archive")]
	[Description("A standard zip archive containing the comic images.")]
	Zip,
	/// <summary>
	/// The comic-book zip archive
	/// </summary>
	[Display(Name = "CBZ Archive")]
	[Description("A comic-book zip archive containing the comic images.")]
	Cbz
}
