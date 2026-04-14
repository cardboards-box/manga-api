namespace MangaBox.Models.Composites.Import;

using Types;

/// <summary>
/// A manga to be imported
/// </summary>
public class ImportManga
{
    private bool? _nsfw = null;

    /// <summary>
    /// The title of the manga
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The original source ID of the manga
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the provider the manga was loaded through
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the manga on the original source site
    /// </summary>
    [JsonPropertyName("homePage")]
    public string HomePage { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the manga cover image
    /// </summary>
    [JsonPropertyName("cover")]
    public string Cover { get; set; } = string.Empty;

    /// <summary>
    /// The description of the manga
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; } = string.Empty;

    /// <summary>
    /// Alternate descriptions for the manga
    /// </summary>
    [JsonPropertyName("altDescriptions")]
    public string[] AltDescriptions { get; set; } = [];

    /// <summary>
    /// Alternate titles for the manga
    /// </summary>
    [JsonPropertyName("altTitles")]
    public string[] AltTitles { get; set; } = [];

    /// <summary>
    /// The tags of the manga
    /// </summary>
    [JsonIgnore]
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// The authors of the manga
    /// </summary>
    [JsonPropertyName("authors")]
    public string[] Authors { get; set; } = [];

    /// <summary>
    /// The artists of the manga
    /// </summary>
    [JsonPropertyName("artists")]
    public string[] Artists { get; set; } = [];

    /// <summary>
    /// The uploaders of the manga
    /// </summary>
    [JsonPropertyName("uploaders")]
    public string[] Uploaders { get; set; } = [];

    /// <summary>
    /// The rating of the manga
    /// </summary>
    [JsonPropertyName("rating")]
    public ContentRating? Rating { get; set; } = null;

    /// <summary>
    /// The chapters of the manga to be imported
    /// </summary>
    [JsonPropertyName("chapters")]
    public List<ImportChapter> Chapters { get; set; } = [];

    /// <summary>
    /// Whether or not the manga is NSFW (will be inferred from <see cref="Rating"/> if not explicitly set)
    /// </summary>
    [JsonPropertyName("nsfw")]
    public bool? Nsfw
    {
        get => _nsfw ?? (Rating is null ? null : (Rating != ContentRating.Safe));
        set => _nsfw = value;
    }

    /// <summary>
    /// The various attributes of the manga to be imported
    /// </summary>
    [JsonPropertyName("attributes")]
    public List<ImportAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// The tags of the manga to be imported (will be generated from <see cref="Tags"/> if not explicitly set)
    /// </summary>
    [JsonPropertyName("tags")]
    public ImportTag[] MangaTags
    {
        get => [.. Tags.Select(t => new ImportTag { Name = t }).DistinctBy(t => t.Slug)];
        set => Tags = [.. value.Select(t => t.Name).Distinct()];
    }

    /// <summary>
    /// The referer to include when making XHR requests
    /// </summary>
    [JsonPropertyName("referer")]
    public string? Referer { get; set; }

    /// <summary>
    /// The date the source manga was created
    /// </summary>
    [JsonPropertyName("sourceCreated")]
    public DateTime? SourceCreated { get; set; }

    /// <summary>
    /// Whether or not the chapter number resets whenever the volume number changes
    /// </summary>
    [JsonPropertyName("ordinalVolumeReset")]
    public bool OrdinalVolumeReset { get; set; } = false;

    /// <summary>
    /// The optional legacy ID of the manga, if it existed in a previous version of the API
    /// </summary>
    [JsonPropertyName("legacyId")]
    public int? LegacyId { get; set; }
}