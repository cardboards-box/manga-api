namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for chapter endpoints
/// </summary>
public class ChapterController(
	IDbService _db,
	IMangaLoaderService _loader,
	IImageService _images,
	ILogger<ChapterController> logger) : BaseController(logger)
{
	/// <summary>
	/// Fetches a chapter by it's ID
	/// </summary>
	/// <param name="id">The ID of the chapter</param>
	/// <param name="refetch">Whether or not to force refresh the page links</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The chapter data</returns>
	[HttpGet, Route("chapter/{id}")]
	[ProducesBox<MangaBoxType<MbChapter>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Fetch([FromRoute] string id, CancellationToken token, [FromQuery] bool refetch = false) => Box(async () =>
	{
		if (!this.GetProfileId().HasValue)
			return Boxed.NotFound(nameof(MbChapter), "Chapter was not found");

		if (!Guid.TryParse(id, out var cid))
			return Boxed.Bad("Chapter ID is not a valid GUID.");

		return await _loader.Pages(cid, refetch, token);
	});


	/// <summary>
	/// Downloads a zip of the chapter images
	/// </summary>
	/// <param name="id">The ID of the chapter</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The image data or the error</returns>
	[HttpGet, Route("chapter/{id}/download")]
	[ProducesError(500), ProducesError(404), ProducesError(400)]
	[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
	[ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
	public async Task<IActionResult> Download([FromRoute] string id, CancellationToken token)
	{
		if (!this.GetProfileId().HasValue)
			return await Box(() => Boxed.NotFound("Chapter was not found"));

		if (!Guid.TryParse(id, out var guid))
			return await Box(() => Boxed.Bad($"Invalid image ID: {id}"));

		var result = await _images.Download(guid, token);
		if (!string.IsNullOrEmpty(result.Error) ||
			result.Stream is null)
			return await Box(() => Boxed.Exception(result.Error ?? "Zip stream is missing"));

		return File(result.Stream, result.MimeType ?? "application/zip", result.FileName);
	}

	/// <summary>
	/// Book marks the given pages in the chapter
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The updated progress</returns>
	[HttpPut, Route("chapter/bookmarks")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Bookmark([FromBody] BookmarkRequest request) => Box(async () =>
	{
		var id = this.GetProfileId();
		if (!id.HasValue) return Boxed.Unauthorized("User is not authenticated.");

		var result = await _db.ChapterProgress.UpdateBookmarks(id.Value, request.ChapterId, request.Bookmarks);
		if (result is null) return Boxed.Exception("Failed to update bookmarks.");

		return Boxed.Ok(result);
	});

	/// <summary>
	/// Updates the progress for the given chapter
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The updated progress</returns>
	[HttpPut, Route("chapter/progress")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Progress([FromBody] ProgressOrdinalRequest request) => Box(async () =>
	{
		var id = this.GetProfileId();
		if (!id.HasValue) return Boxed.Unauthorized("User is not authenticated.");

		var result = await _db.ChapterProgress.UpdateOrdinal(id.Value, request.ChapterId, request.PageOrdinal);
		if (result is null) return Boxed.Exception("Failed to update page ordinal.");

		return Boxed.Ok(result);
	});

	/// <summary>
	/// The body of the request to create bookmarks
	/// </summary>
	/// <param name="ChapterId">The ID of the chapter</param>
	/// <param name="Bookmarks">The bookmarked pages</param>
	public record class BookmarkRequest(
		[property: JsonPropertyName("chapterId")] Guid ChapterId,
		[property: JsonPropertyName("bookmarks")] int[] Bookmarks);

	/// <summary>
	/// The body of the request to update the progress ordinal
	/// </summary>
	/// <param name="ChapterId">The ID of the chapter</param>
	/// <param name="PageOrdinal">The page ordinal to update</param>
	public record class ProgressOrdinalRequest(
		[property: JsonPropertyName("chapterId")] Guid ChapterId,
		[property: JsonPropertyName("pageOrdinal")] int? PageOrdinal);
}
