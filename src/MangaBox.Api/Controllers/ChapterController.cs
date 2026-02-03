namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for chapter endpoints
/// </summary>
public class ChapterController(
	IDbService _db,
	IMangaLoaderService _loader,
	ILogger<ChapterController> logger) : BaseController(logger)
{
	/// <summary>
	/// Fetches a chapter by it's ID
	/// </summary>
	/// <param name="id">The ID of the chapter</param>
	/// <param name="refetch">Whether or not to force refresh the page links</param>
	/// <returns>The chapter data</returns>
	[HttpGet, Route("chapter/{id}")]
	[ProducesBox<MangaBoxType<MbChapter>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Fetch([FromRoute] string id, [FromQuery] bool refetch = false) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var cid))
			return Boxed.Bad("Chapter ID is not a valid GUID.");

		return await _loader.Pages(cid, refetch);
	});

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
