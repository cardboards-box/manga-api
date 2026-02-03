namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for manga endpoints
/// </summary>
public class MangaController(
	IDbService _db,
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
		filter.ProfileId = this.GetProfileId();
		if (!filter.ProfileId.HasValue && 
			(filter.States.Length != 0 ||
			filter.Order.ContainsKey(MangaOrderBy.LastRead)))
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
	/// Fetches all of the chapters of the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The chapters or the error</returns>
	[HttpGet, Route("manga/{id}/chapters")]
	[ProducesBox<MbChapter[]>, ProducesError(400)]
	public Task<IActionResult> Chapters([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		var chapters = await _db.Chapter.ByManga(mid);
		return Boxed.Ok(chapters);
	});

	/// <summary>
	/// Refreshes a manga by it's ID from it's source
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The manga or the error</returns>
	[HttpGet, Route("manga/{id}/refresh")]
	[ProducesBox<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Refresh([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var mid))
			return Boxed.Bad("Manga ID is not a valid GUID.");

		return await _loader.Refresh(this.GetProfileId(), mid);
	});

	/// <summary>
	/// Loads a manga from it's source
	/// </summary>
	/// <param name="request">The request to load a manga</param>
	/// <returns>The manga or the error</returns>
	[HttpPost, Route("manga/load")]
	[ProducesBox<MangaBoxType<MbManga>>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Load([FromBody] LoadRequest request) => Box(async () =>
	{
		return await _loader.Load(this.GetProfileId(), request.Url, request.Force);
	});

	/// <summary>
	/// Favourites the given manga
	/// </summary>
	/// <param name="id">The ID of the manga to favorite</param>
	/// <returns>The manga progress</returns>
	[HttpGet, Route("manga/{id}/favorite")]
	[ProducesBox<MbMangaProgress>, ProducesError(400), ProducesError(401)]
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
	[ProducesBox<MbMangaProgress>, ProducesError(400), ProducesError(401)]
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
	/// The request body to load a manga
	/// </summary>
	/// <param name="Url">The url of the manga to load</param>
	/// <param name="Force">Whether to force the load even if the manga already exists</param>
	public record class LoadRequest(
		[property: JsonPropertyName("url")] string Url,
		[property: JsonPropertyName("force")] bool Force = false);
}
