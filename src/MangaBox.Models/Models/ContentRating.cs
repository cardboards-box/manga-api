namespace MangaBox.Models;

/// <summary>
/// Represents a content rating for a series
/// </summary>
[Table("mb_content_ratings")]
public class ContentRating : DbObject
{
    public const string SAFE = "Safe";
    public const string SUGGESTIVE = "Suggestive";
    public const string EROTICA = "Erotica";
    public const string PORNOGRAPHIC = "Pornographic";

    /// <summary>
    /// The name of the rating
    /// </summary>
    [Column("name", Unique = true)]
    public required string Name { get; set; }

    /// <summary>
    /// The description of the rating
    /// </summary>
    [Column("description")]
    public required string Description { get; set; }

    /// <summary>
    /// The color of the tag
    /// </summary>
    [Column("tag_color")]
    public required string TagColor { get; set; }

    /// <summary>
    /// Whether the rating is safe for work or not
    /// </summary>
    [Column("explicit")]
    public bool Explicit { get; set; }

    /// <summary>
    /// Whether the result is pornographic or not (Hidden by default)
    /// </summary>
    [Column("pornographic")]
    public bool Pornographic { get; set; }
}
