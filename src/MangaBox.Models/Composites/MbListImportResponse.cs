namespace MangaBox.Models.Composites;

/// <summary>
/// The response from importing a list from an external source
/// </summary>
public class MbListImportResponse
{
	/// <summary>
	/// The failures for the import request
	/// </summary>
	[JsonPropertyName("failures")]
	public Dictionary<string, string> Failures { get; set; } = [];

	/// <summary>
	/// The list that was imported, if the import was successful
	/// </summary>
	[JsonPropertyName("list")]
	public MangaBoxType<MbList>? List { get; set; }
}
