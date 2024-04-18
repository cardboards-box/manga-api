namespace MangaBox.Models;

/// <summary>
/// Represents the relationship between a series and a person
/// </summary>
[Table("mb_series_people")]
public class SeriesPeople : DbObject
{
    /// <summary>
    /// The ID of the <see cref="Series"/> that this person is involved in
    /// </summary>
    [Column("series_id", Unique = true)]
    public required Guid SeriesId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Person"/> that is involved in the series
    /// </summary>
    [Column("person_id", Unique = true)]
    public required Guid PersonId { get; set; }

    /// <summary>
    /// The type of relationship this person has with the series
    /// </summary>
    [Column("type", Unique = true)]
    public required PersonRelationship Type { get; set; }
}
