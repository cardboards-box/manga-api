namespace MangaBox.Models;

using Types;

using static Constants;

/// <summary>
/// All of the source providers that MangaBox supports
/// </summary>
[Table("mb_sources")]
[InterfaceOption(nameof(MbSource))]
public class MbSource : MbDbObject, IDbCacheTable
{
	/// <summary>
	/// The unique slug of the source
	/// </summary>
	[Column("slug", Unique = true)]
	[MaxLength(MAX_NAME_LENGTH), MinLength(MIN_NAME_LENGTH), Required]
	[JsonPropertyName("slug")]
	public string Slug { get; set; } = string.Empty;

	/// <summary>
	/// The name of the source
	/// </summary>
	[Column("name")]
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The base URL to use for the source
	/// </summary>
	[Column("base_url")]
	[MaxLength(MAX_URL_LENGTH), Url, Required]
	[JsonPropertyName("baseUrl")]
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Whether or not the source is hidden from the public
	/// </summary>
	[Column("is_hidden")]
	[JsonPropertyName("isHidden")]
	public bool IsHidden { get; set; } = false;

	/// <summary>
	/// Whether or not the source is enabled
	/// </summary>
	[Column("enabled")]
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// The default content rating for the source
	/// </summary>
	[Column("default_rating")]
	[JsonPropertyName("defaultRating")]
	public ContentRating DefaultRating { get; set; } = ContentRating.Safe;

	/// <summary>
	/// The referer to add as a header when making requests
	/// </summary>
	[Column("referer")]
	[MaxLength(MAX_URL_LENGTH), Url]
	[JsonIgnore]
	public string? Referer { get; set; }

	/// <summary>
	/// The optional user-agent to use when making requests
	/// </summary>
	[Column("user_agent")]
	[JsonIgnore]
	public string? UserAgent { get; set; }

	/// <summary>
	/// The headers to attach to any image request
	/// </summary>
	[Column("headers"), InnerValid]
	[JsonIgnore]
	public MbHeader[] Headers { get; set; } = [];
}
