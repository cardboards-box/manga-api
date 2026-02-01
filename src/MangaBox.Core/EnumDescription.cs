namespace MangaBox.Core;

/// <summary>
/// Represents the description of an enum value
/// </summary>
/// <param name="Name">The name of the enum</param>
/// <param name="Description">The description of the enum</param>
/// <param name="Value">The value of the enum</param>
/// <param name="TypeName">The name of the type</param>
public record class EnumDescription(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("description")] string? Description,
	[property: JsonPropertyName("value")] long Value,
	[property: JsonPropertyName("typeName")] string TypeName);
