namespace MangaBox.Models;

/// <summary>
/// Represents an external link to a social platform or website
/// </summary>
[Type("mb_external_link")]
public class ExternalLink
{
    /// <summary>
    /// The name of the platform like: Discord, Website, Twitter, etc.
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// The URL to the platform in the current context
    /// </summary>
    public required string Url { get; set; }
}
