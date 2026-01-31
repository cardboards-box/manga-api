namespace MangaBox.Models;

/// <summary>
/// Represents a manga series
/// </summary>
[Table("mb_series")]
public class Series : Auditable
{
    /// <summary>
    /// The ID of the <see cref="Provider"/> that this series is from
    /// </summary>
    [Column("provider_id", Unique = true)]
    public required Guid ProviderId { get; set; }

    /// <summary>
    /// The ID of the series from the source
    /// </summary>
    [Column("source_id", Unique = true)]
    public required string SourceId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Image"/> to use as a cover
    /// </summary>
    [Column("cover_id")]
    public required Guid? CoverId { get; set; }

    /// <summary>
    /// The ID of the <see cref="ContentRating"/> for this series
    /// </summary>
    [Column("rating_id")]
    public required Guid RatingId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Tag"/>s for this series
    /// </summary>
    [Column("tags")]
    public required Guid[] Tags { get; set; } = [];

    /// <summary>
    /// The title of the manga from the source
    /// </summary>
    [Column("title")]
    public required string Title { get; set; }

    /// <summary>
    /// The overridable title to display on the website
    /// </summary>
    [Column("display_title", ExcludeUpdates = true)]
    public required string? DisplayTitle { get; set; }

    /// <summary>
    /// Any alternative titles for the manga
    /// </summary>
    [Column("alt_titles")]
    public required string[] AltTitles { get; set; } = [];

    /// <summary>
    /// The description of the manga
    /// </summary>
    [Column("description")]
    public required string Description { get; set; }

    /// <summary>
    /// The URL of the manga on the source
    /// </summary>
    [Column("url")]
    public required string Url { get; set; }

    /// <summary>
    /// The status of the series (like ongoing, completed, etc)
    /// </summary>
    [Column("status")]
    public required SeriesStatus Status { get; set; } = SeriesStatus.OnGoing;

    /// <summary>
    /// Whether chapter numbers reset when a new volume starts
    /// </summary>
    [Column("ordinals_reset")]
    public required bool OrdinalsReset { get; set; }

    /// <summary>
    /// The date that the series was created on the source
    /// </summary>
    [Column("source_created")]
    public required DateTime? SourceCreated { get; set; }
}
