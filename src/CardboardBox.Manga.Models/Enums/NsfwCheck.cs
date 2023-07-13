namespace CardboardBox.Manga.Models;

/// <summary>
/// Represents the desired NSFW state of filtered manga
/// </summary>
public enum NsfwCheck
{
    /// <summary>
    /// Manga contains no NSFW tags and is not marked as Suggestive, Pornographic, or Erotic
    /// </summary>
    Sfw = 0,
    /// <summary>
    /// Manga contains one or more NSFW tags or is marked as Suggestive, Pornographic, or Erotic
    /// </summary>
    Nsfw = 1,
    /// <summary>
    /// Doesn't matter, returns all results
    /// </summary>
    DontCare = 2
}
