using MangaBox.Match;

namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for reverse image search endpoints
/// </summary>
public class ReverseSearchController(
	IReverseImageSearchService _search,
	ILogger<ReverseSearchController> logger) : BaseController(logger)
{
	/// <summary>
	/// Searches for any matching manga
	/// </summary>
	/// <param name="url">The URL of the image</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	[HttpGet, Route("reverse-search")]
	[ProducesArray<ImageSearchResult>, ProducesError(400)]
	public Task<IActionResult> Search([FromQuery] string url, CancellationToken token) => Box(async () =>
	{
		if (string.IsNullOrWhiteSpace(url))
			return Boxed.Bad("URL cannot be empty.");
		return await _search.Search(url, token);
	});

	/// <summary>
	/// Searches for any matching manga
	/// </summary>
	/// <param name="file">The file to search</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	[HttpPost, Route("reverse-search")]
	[ProducesArray<ImageSearchResult>, ProducesError(400)]
	public Task<IActionResult> Search(IFormFile file, CancellationToken token) => Box(async () =>
	{
		return await _search.Search(file.OpenReadStream(), file.FileName, token);
	});
}
