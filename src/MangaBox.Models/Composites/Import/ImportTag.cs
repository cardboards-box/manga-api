namespace MangaBox.Models.Composites.Import;

/// <summary>
/// A tag to be imported with a manga
/// </summary>
public class ImportTag
{
    /// <summary>
    /// The name of the tag
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The optional slug of the tag (will be generated from the name if not provided)
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug
    {
        get => field ??= MbTag.GenerateSlug(Name);
        set => field = MbTag.GenerateSlug(value);
    }
}