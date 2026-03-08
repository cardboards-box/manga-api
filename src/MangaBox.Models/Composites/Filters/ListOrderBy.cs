namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The order criteria for list searches
/// </summary>
public enum ListOrderBy
{
	/// <summary>
	/// When the list was created
	/// </summary>
	[Display(Name = "Created At")]
	[Description("When the list was created")]
	CreatedAt = 0,
	/// <summary>
	/// When the list was last updated
	/// </summary>
	[Display(Name = "Updated At")]
	[Description("When the list was last updated")]
	UpdatedAt = 1,
	/// <summary>
	/// The name of the list
	/// </summary>
	[Display(Name = "Name")]
	[Description("The name of the list")]
	Name = 2,
	/// <summary>
	/// Whether or not the list is public
	/// </summary>
	[Display(Name = "Is Public")]
	[Description("Whether or not the list is public")]
	IsPublic = 3,
	/// <summary>
	/// Orders the lists randomly
	/// </summary>
	[Display(Name = "Random")]
	[Description("Orders the lists randomly")]
	Random = 4
}
