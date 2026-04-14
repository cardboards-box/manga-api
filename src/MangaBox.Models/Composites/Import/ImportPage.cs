namespace MangaBox.Models.Composites.Import;

/// <summary>
/// A page to be imported with a chapter
/// </summary>
public class ImportPage()
{
    /// <summary>
    /// The URL of the page image
    /// </summary>
    [JsonPropertyName("page")]
    public string Page { get; set; } = string.Empty;

    /// <summary>
    /// The optional width of the image (in pixels)
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// The optional height of the image (in pixels)
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>
    /// Optional headers to include when fetching the headers
    /// </summary>
    [JsonPropertyName("headers")]
    public List<ImportAttribute> Headers { get; set; } = [];

    /// <summary>
    /// A page to be imported with a chapter
    /// </summary>
    /// <param name="page">The URL of the page image</param>
    /// <param name="width">The optional width of the image (in pixels)</param>
    /// <param name="height">The optional height of the image (in pixels)</param>
    public ImportPage(string page, int? width = null, int? height = null) : this()
    {
        Page = page;
        Width = width;
        Height = height;
    }
}