namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The various types of lists to search for
/// </summary>
public enum ListType
{
	/// <summary>
	/// Lists owned by the requesting profile
	/// </summary>
	[Display(Name = "My lists")]
	[Description("My lists")]
	Mine = 0,

	/// <summary>
	/// Lists that are public
	/// </summary>
	[Display(Name = "Public lists")]
	[Description("Public lists")]
	Public = 1,
}
