namespace MangaBox.Models;

/// <summary>
/// The status of a series
/// </summary>
public enum SeriesStatus
{
    /// <summary>
    /// The series is currently being released
    /// </summary>
    OnGoing = 1,
    /// <summary>
    /// The series has been put on hold
    /// </summary>
    Hiatus = 2,
    /// <summary>
    /// The series has been completed
    /// </summary>
    Completed = 3,
    /// <summary>
    /// The series has been cancelled
    /// </summary>
    Cancelled = 4
}
