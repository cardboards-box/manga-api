namespace MangaBox.Models;

/// <summary>
/// Represents a page in a manga
/// </summary>
[Table("mb_pages")]
public class Page : Orderable
{
    /// <summary>
    /// The ID of the <see cref="Chapter"/>
    /// </summary>
    [Column("chapter_id", Unique = true)]
    public required Guid ChapterId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Image"/>
    /// </summary>
    [Column("image_id", Unique = true)]
    public required Guid ImageId { get; set; }

    /// <summary>
    /// Whether the page is an image, ad, or scanlation group's page
    /// </summary>
    [Column("type")]
    public PageType Type { get; set; } = PageType.Page;
}
