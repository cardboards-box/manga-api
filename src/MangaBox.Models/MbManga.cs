namespace MangaBox.Models;

using Types;

using static Constants;

/// <summary>
/// All of the manga that are present on MangaBox
/// </summary>
[Table("mb_manga")]
[InterfaceOption(nameof(MbManga))]
[Searchable(nameof(Title), nameof(Description), nameof(AltTitles))]
public class MbManga : MbDbObjectLegacy
{
	private bool? _nsfw = null;

	/// <summary>
	/// The series' titles
	/// </summary>
	[Column("title")]
	[JsonPropertyName("title"), MinLength(1)]
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// The series' alternative titles
	/// </summary>
	[Column("alt_titles")]
	[JsonPropertyName("altTitles")]
	public string[] AltTitles { get; set; } = [];

	/// <summary>
	/// The manga's primary description
	/// </summary>
	[Column("description")]
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>
	/// The manga's alternate descriptions
	/// </summary>
	[Column("alt_descriptions")]
	[JsonPropertyName("altDescriptions")]
	public string[] AltDescriptions { get; set; } = [];

	/// <summary>
	/// The manga's source URL
	/// </summary>
	[Column("url")]
	[Required, Url]
	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	/// <summary>
	/// All of the extra attributes for the manga
	/// </summary>
	[Column("attributes")]
	[JsonPropertyName("attributes"), InnerValid]
	public MbAttribute[] Attributes { get; set; } = [];

	/// <summary>
	/// The content rating for the manga
	/// </summary>
	[Column("content_rating")]
	[JsonPropertyName("contentRating")]
	public ContentRating ContentRating { get; set; } = ContentRating.Safe;

	/// <summary>
	/// Whether or not the manga is NSFW
	/// </summary>
	[Column("nsfw")]
	[JsonPropertyName("nsfw")]
	public bool Nsfw
	{
		get => _nsfw ?? (ContentRating != ContentRating.Safe);
		set => _nsfw = value;
	}

	/// <summary>
	/// The ID of the source provider the series belongs to
	/// </summary>
	[Column("source_id", Unique = true), Fk<MbSource>]
	[Required]
	[JsonPropertyName("sourceId")]
	public Guid SourceId { get; set; }

	/// <summary>
	/// The unique ID of the series on the original source
	/// </summary>
	[Column("original_source_id", Unique = true)]
	[Required]
	[JsonPropertyName("originalSourceId")]
	public string OriginalSourceId { get; set; } = string.Empty;

	/// <summary>
	/// Whether or not the manga is hidden from the public
	/// </summary>
	[Column("is_hidden", ExcludeUpdates = true)]
	[JsonPropertyName("isHidden")]
	public bool IsHidden { get; set; } = false;

	/// <summary>
	/// The referer to add as a header when making requests
	/// </summary>
	[Column("referer", ExcludeUpdates = true)]
	[MaxLength(MAX_URL_LENGTH), Url]
	[JsonIgnore]
	public string? Referer { get; set; }

	/// <summary>
	/// The optional user-agent to use when making requests
	/// </summary>
	[Column("user_agent", ExcludeUpdates = true)]
	[JsonIgnore]
	public string? UserAgent { get; set; }

	/// <summary>
	/// When the manga was created on the source provider
	/// </summary>
	[Column("source_created", ExcludeUpdates = true)]
	[JsonPropertyName("sourceCreated")]
	public DateTime? SourceCreated { get; set; }

	/// <summary>
	/// Whether or not the chapter ordinals reset for each volume
	/// </summary>
	[Column("ordinal_volume_reset")]
	[JsonPropertyName("ordinalVolumeReset")]
	public bool OrdinalVolumeReset { get; set; } = false;
}
