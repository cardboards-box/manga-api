namespace MangaBox.Models.Composites.Import;

/// <summary>
/// A request to import a manga
/// </summary>
public class ImportRequest
{
    /// <summary>
    /// The ID of the source the manga should be imported against
    /// </summary>
    [JsonPropertyName("sourceId")]
    public Guid SourceId { get; set; }

    /// <summary>
    /// The ID of the profile to impersonate when importing the manga
    /// </summary>
    [JsonPropertyName("profileId")]
    public Guid? ProfileId { get; set; }

    /// <summary>
    /// The manga to be imported
    /// </summary>
    [JsonPropertyName("manga")] 
    public ImportManga Manga { get; set; } = new();
}
