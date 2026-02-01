namespace MangaBox.Models.Types;

/// <summary>
/// Represents an attribute for a manga or chapter
/// </summary>
[Type("mb_attribute")]
[CreateArrayJoin("attributes", nameof(Value))]
public class MbAttribute : IValidator, IDbType
{
	/// <summary>
	/// The name of the attribute
	/// </summary>
	[Required]
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The value of the attribute
	/// </summary>
	[Required]
	[JsonPropertyName("value")]
	public string Value { get; set; } = string.Empty;
}
