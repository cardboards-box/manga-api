namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for manga endpoints
/// </summary>
public class MangaController(
	IDbService _db,
	IVolumeService _volume,
	IMangaLoaderService _loader,
	ILogger<MangaController> logger) : BaseController(logger)
{
	/// <summary>
	/// Searches for manga based on the given filter
	/// </summary>
	/// <param name="filter">The filter to search for</param>
	/// <returns>The paginated search results</returns>
	[HttpPost, Route("manga")]
	[ProducesPaged<MangaBoxType<MbManga>>, ProducesError(400)]
	public Task<IActionResult> Search([FromBody] MangaSearchFilter filter) => Box(async () =>
	{
		if (filter.Page <= 0)
			return Boxed.Bad("Page must be greater than 0.");
		if (filter.Size <= 0 || filter.Size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		filter.ProfileId = this.GetProfileId();
		if (!filter.ProfileId.HasValue && 
			(filter.States.Length != 0 ||
			filter.Order == MangaOrderBy.LastRead))
			return Boxed.Unauthorized("You need to be logged in to use states or last read ordering.");

		var results = await _db.Manga.Search(filter);
		return Boxed.Ok(results.Pages, results.Count, results.Results);
	});

	/// <summary>
	/// Searches for manga based on the given filter
	/// </summary>
	/// <param name="filter">The filter to search for</param>
	/// <returns>The paginated search results</returns>
	[HttpGet, Route("manga")]
	[ProducesPaged<MangaBoxType<MbManga>>, ProducesError(400)]
	public Task<IActionResult> SearchQuery([FromQuery] MangaSearchFilter filter) => Search(filter);

	/// <summary>
	/// Fetches a manga by it's ID
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The manga or the error</returns>
	[HttpGet, Route("manga/{id}")]
	[ProducesBox<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Fetch([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var manga = await _db.Manga.FetchWithRelationships(mid);
		if (manga is null)
			return Boxed.NotFound(nameof(MbManga), "Manga was not found.");

		return Boxed.Ok(manga);
	});

	/// <summary>
	/// Finds the recommended manga
	/// </summary>
	/// <param name="id">The ID of the manga to compare to</param>
	/// <param name="size">The number of related items to return</param>
	/// <returns>The recommended manga</returns>
	[HttpGet, Route("manga/{id}/recommended")]
	[ProducesArray<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Recommended([FromRoute] string id, [FromQuery] int size = 20) => Box(async () =>
	{
		if (size <= 0 || size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var manga = await _db.Manga.Recommended(mid, size);
		return Boxed.Ok(manga);
	});

	/// <summary>
	/// Finds the recommended manga
	/// </summary>
	/// <param name="size">The number of related items to return</param>
	/// <returns>The recommended manga</returns>
	[HttpGet, Route("manga/recommended")]
	[ProducesArray<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> RecommendedProfile([FromQuery] int size = 20) => Box(async () =>
	{
		if (size <= 0 || size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("You must be logged in to use this!");

		var manga = await _db.Manga.RecommendedByProfile(pid.Value, size);
		return Boxed.Ok(manga);
	});

	/// <summary>
	/// Deletes a manga by it's ID
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The number of records deleted</returns>
	[HttpDelete, Route("manga/{id}")]
	[ProducesBox<int>, ProducesError(400), ProducesError(404), ProducesError(401)]
	public Task<IActionResult> Delete([FromRoute] string id) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");

		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");
		var deleted = await _db.Manga.Delete(mid);
		return Boxed.Ok(deleted);
	});

	/// <summary>
	/// Fetches all of the chapters of the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="order">The order to sort the chapters by</param>
	/// <param name="asc">Whether to sort in ascending order</param>
	/// <returns>The chapters or the error</returns>
	[HttpGet, Route("manga/{id}/chapters")]
	[ProducesBox<MangaVolumes>, ProducesError(400)]
	public Task<IActionResult> Chapters(
		[FromRoute] string id,
		[FromQuery] ChapterOrderBy order = ChapterOrderBy.Ordinal,
		[FromQuery] bool asc = true) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		return await _volume.Get(new()
		{
			MangaId = mid,
			Order = order,
			Asc = asc,
		}, this.GetProfileId());
	});

	/// <summary>
	/// Refreshes a manga by it's ID from it's source
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The manga or the error</returns>
	[HttpGet, Route("manga/{id}/refresh")]
	[ProducesBox<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404), ProducesError(401)]
	public Task<IActionResult> Refresh([FromRoute] string id, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue) return Boxed.Unauthorized("User is not authenticated.");

		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		return await _loader.Refresh(this.GetProfileId(), mid, token);
	});

	/// <summary>
	/// Loads a manga from it's source
	/// </summary>
	/// <param name="request">The request to load a manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The manga or the error</returns>
	[HttpPost, Route("manga/load")]
	[ProducesBox<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404), ProducesError(401)]
	public Task<IActionResult> Load([FromBody] LoadRequest request, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue) return Boxed.Unauthorized("User is not authenticated.");

		return await _loader.Load(this.GetProfileId(), request.Url, request.Force, token);
	});

	/// <summary>
	/// Favourites the given manga
	/// </summary>
	/// <param name="id">The ID of the manga to favorite</param>
	/// <returns>The manga progress</returns>
	[HttpGet, Route("manga/{id}/favorite")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Favorite([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var profileId = this.GetProfileId();
		if (!profileId.HasValue)
			return Boxed.Unauthorized("You need to be logged in to favorite manga.");

		var progress = await _db.MangaProgress.Favourite(profileId.Value, mid, true);
		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Unfavourites the given manga
	/// </summary>
	/// <param name="id">The ID of the manga to unfavorite</param>
	/// <returns>The manga progress</returns>
	[HttpDelete, Route("manga/{id}/favorite")]
	[ProducesBox<MangaBoxType<MbMangaProgress>>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Unfavorite([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var profileId = this.GetProfileId();
		if (!profileId.HasValue)
			return Boxed.Unauthorized("You need to be logged in to favorite manga.");

		var progress = await _db.MangaProgress.Favourite(profileId.Value, mid, false);
		return Boxed.Ok(progress);
	});

	/// <summary>
	/// Recomputes the extension data for the given manga
	/// </summary>
	/// <param name="ids">The IDs of the manga to recompute</param>
	/// <param name="since">The number of days old the manga data should be to be updated</param>
	/// <returns>The updated records</returns>
	[HttpGet, Route("manga/recompute")]
	[ProducesArray<MbMangaExt[]>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Recompute([FromQuery] Guid[]? ids = null, [FromQuery] double? since = null) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");

		List<MbMangaExt> output = [];
		if (ids is not null && ids.Length > 0)
			output.AddRange(await _db.MangaExt.Update(ids));
		if (since.HasValue)
			output.AddRange(await _db.MangaExt.Update(since.Value));

		if ((ids is null || ids.Length == 0) && !since.HasValue)
			output.AddRange(await _db.MangaExt.MassUpdate());

		return Boxed.Ok(output.ToArray());
	});

	/// <summary>
	/// Sets the display title of the given manga
	/// </summary>
	/// <param name="request">The body of the request</param>
	/// <returns>The response</returns>
	[HttpPut, Route("manga/display-title")]
	[ProducesBox<MbMangaExt>, ProducesError(400), ProducesError(404), ProducesError(401)]
	public Task<IActionResult> Set([FromBody] SetDisplayRequest request) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");

		var updated = await _db.MangaExt.SetDisplayTitle(request.MangaId, request.Title);
		return Boxed.Ok(updated);
	});

	/// <summary>
	/// The request body to load a manga
	/// </summary>
	/// <param name="Url">The url of the manga to load</param>
	/// <param name="Force">Whether to force the load even if the manga already exists</param>
	public record class LoadRequest(
		[property: JsonPropertyName("url")] string Url,
		[property: JsonPropertyName("force")] bool Force = false);

	/// <summary>
	/// The request to set the display title for the manga
	/// </summary>
	/// <param name="MangaId">The ID of the manga</param>
	/// <param name="Title">The display title to set</param>
	public record class SetDisplayRequest(
		[property: JsonPropertyName("mangaId")] Guid MangaId,
		[property: JsonPropertyName("display")] string? Title);
}
