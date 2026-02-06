namespace MangaBox.Match;

/// <summary>
/// A service for performing image searches
/// </summary>
public interface IImageSearchService
{
	/// <summary>
	/// The type of source this represents
	/// </summary>
	RISServices Type { get; }

	/// <summary>
	/// Search for results by the image 
	/// </summary>
	/// <param name="url">The image URL</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	IAsyncEnumerable<ImageSearchResult> Search(string url, CancellationToken token);

	/// <summary>
	/// Search for results by the image
	/// </summary>
	/// <param name="stream">The stream of the image</param>
	/// <param name="fileName">The file name of the image</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	IAsyncEnumerable<ImageSearchResult> Search(MemoryStream stream, string fileName, CancellationToken token);
}
