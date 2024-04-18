namespace MangaBox.Models;

/// <summary>
/// The type of page that is being displayed
/// </summary>
public enum PageType
{
    /// <summary>
    /// A page in the manga
    /// </summary>
    Page = 1,
    /// <summary>
    /// An ad or other non-manga content that doesn't relate to a specific group
    /// </summary>
    Advertisement = 2,
    /// <summary>
    /// The scanlation group's cover page
    /// </summary>
    GroupPage = 3,
}
