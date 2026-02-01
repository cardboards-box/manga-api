namespace MangaBox.Models.Types;

/// <summary>
/// Indicates that a user advisory is recommended
/// </summary>
public enum ContentRating
{
	/// <summary>
	/// No advisory is necessary
	/// </summary>
	/// <remarks>Rated E for everyone</remarks>
	[Display(Name = "Safe")]
	[Description("No advisory is necessary")]
	Safe = 0,
	/// <summary>
	/// Content contains suggestive material that might not be safe for work
	/// </summary>
	[Display(Name = "Suggestive")]
	[Description("Content contains suggestive material that might not be safe for work")]
	Suggestive = 1,
	/// <summary>
	/// Content contains sexual themes or censored nudity (without explicit sexual acts)
	/// </summary>
	/// <remarks>NSFW</remarks>
	[Display(Name = "Erotica")]
	[Description("Content contains sexual themes or censored nudity (without explicit sexual acts)")]
	Erotica = 2,
	/// <summary>
	/// Content contains explicit sexual acts or nudity
	/// </summary>
	/// <remarks>NSFW - Requires 18+ consent</remarks>
	[Display(Name = "Pornographic")]
	[Description("Content contains explicit sexual acts or nudity")]
	Pornographic = 3,
}
