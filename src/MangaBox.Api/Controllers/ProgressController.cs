namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for progress endpoints
/// </summary>
public class ProgressController(
	IDbService _db,
	ILogger<ProgressController> logger) : BaseController(logger)
{
	/// <summary>
	/// Removes all progress from the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The progress for the given manga</returns>
	[HttpDelete, Route("progress/{id}/read")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Delete([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");
		var progress = await _db.MangaProgress.SetProgress(pid.Value, mid, false);
		if (progress is null) 
			return Boxed.NotFound("No progress found for the specified manga.");
		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Marks the entire manga as read for the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The progress for the given manga</returns>
	[HttpGet, Route("progress/{id}/read")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401), ProducesError(404)]
	public Task<IActionResult> Set([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");
		var progress = await _db.MangaProgress.SetProgress(pid.Value, mid, true);
		if (progress is null) 
			return Boxed.NotFound("No progress found for the specified manga.");
		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Fetches the progress of the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The manga progress or an error if not found</returns>
	[HttpGet, Route("progress/{id}")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401), ProducesError(404)]
	public Task<IActionResult> Fetch([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		var progress = await _db.MangaProgress.Fetch(pid.Value, mid);
		if (progress is null) return Boxed.NotFound("No progress found for the specified manga.");

		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Fetches all of teh manga by the given IDs
	/// </summary>
	/// <param name="ids">The IDs of the manga</param>
	/// <returns>The manga progress or an error if not found</returns>
	[HttpGet, Route("progress")]
	[ProducesArray<MbMangaProgress>, ProducesError(401)]
	public Task<IActionResult> Get([FromQuery] string[] ids) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		var mids = ids.Select(t => Guid.TryParse(t, out var mid) ? mid : (Guid?)null)
			.Where(t => t.HasValue)
			.Select(t => t!.Value)
			.ToArray();

		var progress = await _db.MangaProgress.FetchByManga(pid.Value, mids);
		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Updates the progress for the given chapter
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The updated progress</returns>
	[HttpPut, Route("progress")]
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
	/// The body of the request to update the progress ordinal
	/// </summary>
	/// <param name="ChapterId">The ID of the chapter</param>
	/// <param name="PageOrdinal">The page ordinal to update</param>
	public record class ProgressOrdinalRequest(
		[property: JsonPropertyName("chapterId")] Guid ChapterId,
		[property: JsonPropertyName("pageOrdinal")] int? PageOrdinal);
}
