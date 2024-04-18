namespace MangaBox.Models;

/// <summary>
/// Represents a tag for a series
/// </summary>
[Table("mb_tags")]
public class Tag : DbObject
{
    /// <summary>
    /// The lower case version of the tag name
    /// </summary>
    [Column("name", Unique = true)]
    public required string Name { get; set; }

    /// <summary>
    /// The display name of the tag
    /// </summary>
    [Column("display")]
    public required string Display { get; set; }

    /// <summary>
    /// Whether the tag is safe for work or not
    /// </summary>
    [Column("explicit")]
    public bool Explicit { get; set; }
}
