namespace MangaBox.Models;

/// <summary>
/// How the manga are resolved from the source
/// </summary>
public enum SourceType
{
    /// <summary>
    /// JSON content return from an API
    /// </summary>
    Json = 0,
    /// <summary>
    /// HTML web scraping
    /// </summary>
    Html = 1,
    /// <summary>
    /// XML content return from an API
    /// </summary>
    Xml = 2,
    /// <summary>
    /// Something else
    /// </summary>
    Other = 99,
}
