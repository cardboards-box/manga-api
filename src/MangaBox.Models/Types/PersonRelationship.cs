namespace MangaBox.Models;

/// <summary>
/// Represents the type of relationship between a series and a person
/// </summary>
public enum PersonRelationship
{
    /// <summary>
    /// The person who originally created the series
    /// </summary>
    Author = 1,
    /// <summary>
    /// The person who drew the series
    /// </summary>
    Artist = 2,
    /// <summary>
    /// The group that translated the series
    /// </summary>
    Group = 3,
}
