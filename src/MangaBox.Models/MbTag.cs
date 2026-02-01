namespace MangaBox.Models;

/// <summary>
/// Represents all of the tags MangaBox supports
/// </summary>
[Table("mb_tags")]
[InterfaceOption(nameof(MbTag))]
public class MbTag : MbDbObject
{
	/// <summary>
	/// The name of the tag
	/// </summary>
	[Column("name", Unique = true)]
	[JsonPropertyName("name"), MinLength(1)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The description of the tag
	/// </summary>
	[Column("description")]
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>
	/// The source that originally loaded this tag
	/// </summary>
	[Column("source_id"), Fk<MbSource>(ignore: true)]
	[JsonPropertyName("sourceId")]
	public Guid SourceId { get; set; }
}
