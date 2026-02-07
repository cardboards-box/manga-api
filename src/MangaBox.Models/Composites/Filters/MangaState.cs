namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The state of a manga as related to a user
/// </summary>
public enum MangaState
{
	/// <summary>
	/// All of the manga the user has marked as a favorite
	/// </summary>
	[Display(Name = "Favorited")]
	[Description("All of the manga you have marked as a favorite")]
	Favorited = 0,
	/// <summary>
	/// All of the manga the user has read completely
	/// </summary>
	[Display(Name = "Completed")]
	[Description("All of the manga you have read completely")]
	Completed = 1,
	/// <summary>
	/// All of the manga the user is currently reading but hasn't completed yet
	/// </summary>
	[Display(Name = "In Progress")]
	[Description("All of the manga you are currently reading but haven't completed yet")]
	InProgress = 2,
	/// <summary>
	/// All of the manga the user has book marked
	/// </summary>
	[Display(Name = "Bookmarked")]
	[Description("All of the manga you have book marked")]
	Bookmarked = 3
}
