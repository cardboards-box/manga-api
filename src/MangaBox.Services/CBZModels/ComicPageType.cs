namespace MangaBox.Services.CBZModels;

/// <summary>
/// Known ComicInfo page types.
/// </summary>
public enum ComicPageType
{
	/// <summary>Front cover.</summary>
	FrontCover,

	/// <summary>Inner cover.</summary>
	InnerCover,

	/// <summary>Back cover.</summary>
	BackCover,

	/// <summary>Story page.</summary>
	Story,

	/// <summary>Advertisement page.</summary>
	Advertisement,

	/// <summary>Credits page.</summary>
	Credits,

	/// <summary>Table of contents page.</summary>
	TableOfContents,

	/// <summary>Letters page.</summary>
	Letters,

	/// <summary>Preview page.</summary>
	Preview,

	/// <summary>Other / miscellaneous page.</summary>
	Other
}
