namespace MangaBox.Models;

/// <summary>
/// The volumes of this series
/// </summary>
[Table("mb_volumes")]
public class Volume : Orderable
{
    /// <summary>
    /// The ID of the <see cref="Series"/> this volume belongs to
    /// </summary>
    [Column("series_id", Unique = true)]
    public required Guid SeriesId { get; set; }

    /// <summary>
    /// The ordinal of this volume in the series
    /// </summary>
    [Column("ordinal", Unique = true)]
    public override required double Ordinal { get; set; }

    /// <summary>
    /// The title of the volume
    /// </summary>
    /// <remarks>Most likely to just be "Volume {Ordinal}"</remarks>
    [Column("title")]
    public required string Title { get; set; }
}
