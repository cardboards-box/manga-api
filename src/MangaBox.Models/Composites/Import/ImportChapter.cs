namespace MangaBox.Models.Composites.Import;

/// <summary>
/// A chapter to be imported with a manga
/// </summary>
public class ImportChapter
{
    /// <summary>
    /// The optional title of the chapter
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The URL of the chapter
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The original source ID of the chapter
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The chapter number
    /// </summary>
    [JsonPropertyName("number")]
    public double Number { get; set; }

    /// <summary>
    /// The volume number the chapter belongs to
    /// </summary>
    [JsonPropertyName("volume")]
    public double? Volume { get; set; }

    /// <summary>
    /// The external URL of the chapter, if it's hosted on an external site
    /// </summary>
    [JsonPropertyName("externalUrl")]
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// The language of the chapter (will default to EN)
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// Optional attributes for the chapter
    /// </summary>
    [JsonPropertyName("attributes")]
    public List<ImportAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// The optional legacy ID of the chapter, if it existed in a previous version of the API
    /// </summary>
    [JsonPropertyName("legacyId")]
    public int? LegacyId { get; set; }

    /// <summary>
    /// The optional pages for the chapter
    /// </summary>
    [JsonPropertyName("pages")]
    public List<ImportPage> Pages { get; set; } = [];
}