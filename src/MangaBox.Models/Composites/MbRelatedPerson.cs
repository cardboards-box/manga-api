namespace MangaBox.Models.Composites;

using Types;

/// <summary>
/// Represents a person related to a manga with a specific relationship type
/// </summary>
[Composite]
[InterfaceOption(nameof(MbRelatedPerson))]
public class MbRelatedPerson : MbPersonBase
{
	/// <summary>
	/// The type of relationship
	/// </summary>
	[JsonPropertyName("type")]
	public RelationshipType Type { get; set; }
}
