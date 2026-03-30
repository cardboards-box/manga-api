namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The order criteria for log searches
/// </summary>
public enum LogOrderBy
{
	/// <summary>
	/// When the log was created
	/// </summary>
	[Display(Name = "Created At")]
	[Description("When the log was created")]
	CreatedAt = 0,
	/// <summary>
	/// The level of the log
	/// </summary>
	[Display(Name = "Log Level")]
	[Description("The level of the log")]
	LogLevel = 1,
	/// <summary>
	/// The category of the log
	/// </summary>
	[Display(Name = "Category")]
	[Description("The category of the log")]
	Category = 2,
	/// <summary>
	/// The source of the log
	/// </summary>
	[Display(Name = "Source")]
	[Description("The source of the log")]
	Source = 3,
}
