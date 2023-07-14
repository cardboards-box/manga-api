namespace CardboardBox.Manga.Models;

/// <summary>
/// Represents the state of a manga in the context of a user
/// </summary>
public enum TouchedState
{
    /// <summary>
    /// Eveything (can also be represented by any number other than a listed state)
    /// </summary>
    All = 99,
    /// <summary>
    /// Only the manga the user has favourited.
    /// This is indicated by the presence of a <see cref="DbMangaProgress"/> record.
    /// </summary>
    Favourite = 1,
    /// <summary>
    /// Only the manga the user has read to the completion.
    /// This is indicated by the <see cref="DbMangaProgress.MangaChapterId"/> being equal to the final chapter of the manga.
    /// </summary>
    Completed = 2,
    /// <summary>
    /// Only the manga the user is actively reading.
    /// This is indicated by the <see cref="DbMangaProgress.MangaChapterId"/> not being equal to the final chapter of the manga, but still present.
    /// </summary>
    InProgress = 3,
    /// <summary>
    /// Only the manga the user has bookmarked.
    /// This is indicated by a record in the <see cref="DbMangaBookmark"/> table for the current manga (there can be multiple as bookmarks are by chapter, not manga)
    /// </summary>
    Bookmarked = 4,
    /// <summary>
    /// Everything that is not <see cref="Favourite"/>, <see cref="Completed"/>, <see cref="InProgress"/> or <see cref="Bookmarked"/>
    /// This is indicated by a lack of the other states (other than <see cref="All"/>).
    /// </summary>
    Else = 5,
    /// <summary>
    /// Everything that is <see cref="Favourite"/>, <see cref="Completed"/>, <see cref="InProgress"/> or <see cref="Bookmarked"/>
    /// This is indicated by the presence of any of the other states (other than <see cref="Else"/> and <see cref="All"/>).
    /// </summary>
    Touched = 6
}
