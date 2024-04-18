namespace MangaBox.Models;

/// <summary>
/// Represents an image to be resolved from a URL
/// </summary>
[Table("mb_images")]
public class Image : DbObject
{
    /// <summary>
    /// The ID of the provider the image came from
    /// </summary>
    [Column("provider_id")]
    public required Guid ProviderId { get; set; }

    /// <summary>
    /// Where to find the URL
    /// </summary>
    [Column("url")]
    public required string Url { get; set; }

    /// <summary>
    /// An MD5 hash of the URL
    /// </summary>
    [Column("url_hash", Unique = true)]
    public required string UrlHash { get; set; }

    /// <summary>
    /// The type of image this is supposed to be used for
    /// </summary>
    [Column("type")]
    public required ImageType Type { get; set; }

    /// <summary>
    /// The file name of the image (only resolved after the image is fetched)
    /// </summary>
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>
    /// An MD5 hash of the file contents (only resolved after the image is fetched)
    /// </summary>
    [Column("hash")]
    public string? Hash { get; set; }

    /// <summary>
    /// The size of the image in bytes (only resolved after the image is fetched)
    /// </summary>
    [Column("bytes")]
    public int? Bytes { get; set; }

    /// <summary>
    /// The width of the image (only resolved after the image is fetched)
    /// </summary>
    [Column("width")]
    public int? Width { get; set; }

    /// <summary>
    /// The height of the image (only resolved after the image is fetched)
    /// </summary>
    [Column("height")]
    public int? Height { get; set; }

    /// <summary>
    /// The mime type of the image (only resolved after the image is fetched)
    /// </summary>
    [Column("mime_type")]
    public string? MimeType { get; set; }

    /// <summary>
    /// When the file was resolved
    /// </summary>
    [Column("cached_at")]
    public DateTime? CachedAt { get; set; }

    /// <summary>
    /// When the file cache expires and will be re-fetched
    /// </summary>
    [Column("expires")]
    public DateTime? Expires { get; set; }
}

