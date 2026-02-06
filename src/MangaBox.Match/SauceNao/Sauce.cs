namespace MangaBox.Match.SauceNao;

/// <summary>
/// Represents a sauce response from SauceNao API.
/// </summary>
public class Sauce
{
	/// <summary>
	/// The information about the user making the request
	/// </summary>
	[JsonPropertyName("header")]
	public SauceHeader User { get; set; } = new();

	/// <summary>
	/// The results of the search request
	/// </summary>
	[JsonPropertyName("results")]
	public SauceResult[] Results { get; set; } = [];
}
