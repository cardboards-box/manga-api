namespace MangaBox.Models;

/// <summary>
/// Represents a person who is involved in a series
/// </summary>
[Table("mb_people")]
public class Person : DbObject
{
    /// <summary>
    /// The ID of the provider that this person is from
    /// </summary>
    [Column("provider_id", Unique = true)]
    public required Guid ProviderId { get; set; }

    /// <summary>
    /// The ID of the source for this person
    /// </summary>
    [Column("source_id", Unique = true)]
    public required string SourceId { get; set; }

    /// <summary>
    /// The persons name
    /// </summary>
    [Column("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Any links to social platforms for the person
    /// </summary>
    [Column("links")]
    public required ExternalLink[] Links { get; set; } = [];

    /// <summary>
    /// The primary (or first) type of relationship this person has with a series
    /// </summary>
    [Column("type")]
    public required PersonRelationship Type { get; set; }
}
