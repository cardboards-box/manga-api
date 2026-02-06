namespace MangaBox.Match;

/// <summary>
/// The available image search services
/// </summary>
public enum RISServices
{
	/// <summary>
	/// The custom reverse image search service
	/// </summary>
	[Display(Name = "Match RIS")]
	[Description("The custom reverse image search service")]
	MatchRIS,
	/// <summary>
	/// The Sauce-Nao image search service
	/// </summary>
	[Display(Name = "SauceNao")]
	[Description("The Sauce-Nao image search service")]
	SauceNao,
	/// <summary>
	/// The Google Vision image search service
	/// </summary>
	[Display(Name = "Google Vision")]
	[Description("The Google Vision image search service")]
	GoogleVision,
}
