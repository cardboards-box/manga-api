using MangaBox.Models.Types;

namespace MangaBox.Models;

/// <summary>
/// Represents a chapter associated with a manga
/// </summary>
[Table("mb_chapters")]
[InterfaceOption(nameof(MbChapter))]
public class MbChapter : MbDbObjectLegacy
{
	/// <summary>
	/// The ID of the manga this chapter belongs to
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid MangaId { get; set; }

	/// <summary>
	/// The title of the chapter
	/// </summary>
	[Column("title")]
	[JsonPropertyName("title")]
	public string? Title { get; set; }

	/// <summary>
	/// The URL to the chapter on the source
	/// </summary>
	[Url]
	[Column("url")]
	[JsonPropertyName("url")]
	public string? Url { get; set; }

	/// <summary>
	/// The ID of the chapter on the source
	/// </summary>
	[Column("source_id", Unique = true)]
	[JsonPropertyName("sourceId")]
	[Required]
	public string SourceId { get; set; } = string.Empty;

	/// <summary>
	/// The chapter number
	/// </summary>
	[Column("ordinal")]
	[JsonPropertyName("ordinal")]
	public double Ordinal { get; set; }

	/// <summary>
	/// The volume the chapter belongs to
	/// </summary>
	[Column("volume")]
	[JsonPropertyName("volume")]
	public double? Volume { get; set; }

	/// <summary>
	/// The language the chapter is in
	/// </summary>
	[Column("language")]
	[JsonPropertyName("language")]
	public string Language { get; set; } = string.Empty;

	/// <summary>
	/// The place where you can read the chapter if MB cannot scrape the pages
	/// </summary>
	[Column("external_url")]
	[JsonPropertyName("externalUrl")]
	public string? ExternalUrl { get; set; }

	/// <summary>
	/// The optional attributes for the chapter
	/// </summary>
	[Column("attributes")]
	[JsonPropertyName("attributes"), InnerValid]
	public MbAttribute[] Attributes { get; set; } = [];
}
