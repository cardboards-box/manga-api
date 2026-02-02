namespace MangaBox.Models.Types;

/// <summary>
/// Represents a header for HTTP requests
/// </summary>
[Type("mb_headers")]
[CreateArrayJoin("headers", nameof(Value))]
public class MbHeader : IValidator, IDbType
{
	/// <summary>
	/// The key of the header
	/// </summary>
	[Required]
	[JsonPropertyName("key")]
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// The value of the header
	/// </summary>
	[Required]
	[JsonPropertyName("value")]
	public string Value { get; set; } = string.Empty;
}
