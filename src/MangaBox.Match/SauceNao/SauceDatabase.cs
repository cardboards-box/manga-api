namespace MangaBox.Match.SauceNao;

/// <summary>
/// Represents a database used in SauceNao and its status.
/// </summary>
public class SauceDatabase
{
	/// <summary>
	/// The status of the database
	/// </summary>
	[JsonPropertyName("status")]
	public int Status { get; set; }

	/// <summary>
	/// The ID of the parent database
	/// </summary>
	[JsonPropertyName("parent_id")]
	public int ParentId { get; set; }

	/// <summary>
	/// The ID of the current database
	/// </summary>
	[JsonPropertyName("id")]
	public int Id { get; set; }

	/// <summary>
	/// The number of results
	/// </summary>
	[JsonPropertyName("results")]
	public int? Results { get; set; }
}
