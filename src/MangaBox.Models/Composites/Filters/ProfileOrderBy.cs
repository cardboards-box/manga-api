namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The order criteria for profile searches
/// </summary>
public enum ProfileOrderBy
{
	/// <summary>
	/// When the profile was created
	/// </summary>
	[Display(Name = "Created At")]
	[Description("When the profile was created")]
	CreatedAt = 0,
	/// <summary>
	/// When the profile was last updated
	/// </summary>
	[Display(Name = "Updated At")]
	[Description("When the profile was last updated")]
	UpdatedAt = 1,
	/// <summary>
	/// The username of the profile
	/// </summary>
	[Display(Name = "Username")]
	[Description("The username of the profile")]
	Username = 2,
	/// <summary>
	/// The provider of the profile
	/// </summary>
	[Display(Name = "Provider")]
	[Description("The provider of the profile")]
	Provider = 3,
	/// <summary>
	/// The provider ID of the profile
	/// </summary>
	[Display(Name = "Provider ID")]
	[Description("The provider ID of the profile")]
	ProviderId = 4,
	/// <summary>
	/// Whether or not the profile is an administrator
	/// </summary>
	[Display(Name = "Admin")]
	[Description("Whether or not the profile is an administrator")]
	Admin = 5,
	/// <summary>
	/// Whether or not the profile is approved to read
	/// </summary>
	[Display(Name = "Can Read")]
	[Description("Whether or not the profile is approved to read")]
	CanRead = 6,
	/// <summary>
	/// Orders profiles randomly
	/// </summary>
	[Display(Name = "Random")]
	[Description("Orders profiles randomly")]
	Random = 7
}
