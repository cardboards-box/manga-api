namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for progress endpoints
/// </summary>
public class ProgressController(
	IDbService _db,
	ILogger<ProgressController> logger) : BaseController(logger)
{
	/// <summary>
	/// Fetches the progress of the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The manga progress or an error if not found</returns>
	[HttpGet, Route("progress/{id}")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401), ProducesError(404)]
	public Task<IActionResult> ByManga([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		var progress = (await _db.MangaProgress.FetchByManga(pid.Value, mid)).FirstOrDefault();
		if (progress is null) return Boxed.NotFound("No progress found for the specified manga.");

		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Fetches all of teh manga by the given IDs
	/// </summary>
	/// <param name="ids">The IDs of the manga</param>
	/// <returns>The manga progress or an error if not found</returns>
	[HttpGet, Route("progress")]
	[ProducesArray<MangaBoxType<MbMangaProgress>>, ProducesError(401)]
	public Task<IActionResult> ByMangas([FromQuery] string[] ids) => Box(async () =>
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
}
