namespace CardboardBox.Manga.Models;

using Types;

/// <summary>
/// Represents the different type of <see cref="DbMangaAttribute"/>s"/>
/// </summary>
public enum AttributeType
{
    /// <summary>
    /// Not one of the known types (these filters are ignored)
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// The Content Rating for the manga (from MangaDex or shimmed)
    /// </summary>
    ContentRating = 1,
    /// <summary>
    /// The Original Language the manga was written in before TL'd(from MangaDex or shimmed)
    /// </summary>
    OriginalLanguage = 2,
    /// <summary>
    /// The Publication Status of the manga (from MangaDex or shimmed)
    /// </summary>
    Status = 3,
}
