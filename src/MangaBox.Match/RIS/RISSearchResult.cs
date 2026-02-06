namespace MangaBox.Match.RIS;

/// <summary>
/// Represents the results of a request to search for an image
/// </summary>
public class RISSearchResult : RISResult<MatchImage> { }

/// <summary>
/// Represents the results of a request to search for an image with meta-data
/// </summary>
/// <typeparam name="T">The type of meta-data</typeparam>
public class RISSearchResult<T> : RISResult<MatchMetaData<T>> { }