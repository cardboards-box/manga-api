namespace MangaBox.Models;

/// <summary>
/// Represents a chapter of a manga
/// </summary>
[Table("mb_chapters")]
public class Chapter : Orderable
{
    /// <summary>
    /// The ID of the <see cref="Volume"/> this chapter belongs to
    /// </summary>
    [Column("volume_id", Unique = true)]
    public required Guid VolumeId { get; set; }

    /// <summary>
    /// The ID of the chapter from the source
    /// </summary>
    [Column("source_id", Unique = true)]
    public required string SourceId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Person"/> who uploaded this chapter
    /// </summary>
    [Column("uploader_id")]
    public required Guid UploaderId { get; set; }

    /// <summary>
    /// The title of the chapter
    /// </summary>
    /// <remarks>This would normally be "Chapter {Ordinal}" or something</remarks>
    [Column("title")]
    public required string Title { get; set; }

    /// <summary>
    /// The URL of the chapter on the source
    /// </summary>
    [Column("url")]
    public required string Url { get; set; }

    /// <summary>
    /// The URL to the chapter on an external site (if applicable)
    /// </summary>
    [Column("external_url")]
    public required string? ExternalUrl { get; set; }

    /// <summary>
    /// The language code the chapter is in
    /// </summary>
    [Column("language")]
    public required string Language { get; set; }

    /// <summary>
    /// Whether or not the pages have been loaded for this chapter
    /// </summary>
    [Column("pages_loaded")]
    public bool PagesLoaded { get; set; } = false;
}
