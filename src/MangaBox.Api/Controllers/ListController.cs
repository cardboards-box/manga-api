namespace MangaBox.Api.Controllers;

/// <summary>
/// A service for interacting with list endpoints
/// </summary>
public class ListController(
	IDbService _db,
	IListService _list,
	ILogger<ListController> logger) : BaseController(logger)
{
	/// <summary>
	/// Fetches a list by it's ID
	/// </summary>
	/// <param name="id">The ID of the list</param>
	/// <returns>The response</returns>
	[HttpGet, Route("list/{id}")]
	[Produces<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Fetch([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var guid))
			return Boxed.Bad($"Invalid ID: {id}");

		return await _list.Fetch(guid, this.GetProfileId());
	});

	/// <summary>
	/// Fetches all of the lists for the current user
	/// </summary>
	/// <returns>All of the lists for the current user</returns>
	[HttpGet, Route("list/all")]
	[ProducesArray<MbList>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> All() => Box(async () =>
	{
		var items = await _db.List.All(this.GetProfileId());
		return Boxed.Ok(items);
	});

	/// <summary>
	/// Creates a list 
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The response</returns>
	[HttpPost, Route("list")]
	[Produces<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Create([FromBody] MbList.ListCreate request) => Box(() =>
	{
		return _list.Create(request, this.GetProfileId());
	});

	/// <summary>
	/// Updates a list
	/// </summary>
	/// <param name="id">The ID of the list</param>
	/// <param name="request">The request</param>
	/// <returns>The response</returns>
	[HttpPut, Route("list/{id}")]
	[Produces<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Update([FromRoute] string id, [FromBody] MbList.ListUpdate request) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var guid))
			return Boxed.Bad($"Invalid ID: {id}");

		return await _list.Edit(request, guid, this.GetProfileId());
	});

	/// <summary>
	/// Adds an item to the list
	/// </summary>
	/// <param name="listId">The ID of the list</param>
	/// <param name="mangaId">The ID of the manga</param>
	/// <returns>The response</returns>
	[HttpGet, Route("list/{listId}/{mangaId}")]
	[Produces<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Add([FromRoute] string listId, [FromRoute] string mangaId) => Box(async () =>
	{
		if (!Guid.TryParse(listId, out var lid))
			return Boxed.Bad($"Invalid List ID: {listId}");

		if (!Guid.TryParse(mangaId, out var mid))
			return Boxed.Bad($"Invalid Manga ID: {mangaId}");

		return await _list.Link(new(lid, mid, this.GetProfileId()));
	});

	/// <summary>
	/// Removes an item from the list
	/// </summary>
	/// <param name="listId">The ID of the list</param>
	/// <param name="mangaId">The ID of the manga</param>
	/// <returns>The response</returns>
	[HttpDelete, Route("list/{listId}/{mangaId}")]
	[Produces<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Remove([FromRoute] string listId, [FromRoute] string mangaId) => Box(async () =>
	{
		if (!Guid.TryParse(listId, out var lid))
			return Boxed.Bad($"Invalid List ID: {listId}");

		if (!Guid.TryParse(mangaId, out var mid))
			return Boxed.Bad($"Invalid Manga ID: {mangaId}");

		return await _list.Unlink(new(lid, mid, this.GetProfileId()));
	});

	/// <summary>
	/// Searches for lists based on the given filter
	/// </summary>
	/// <param name="filter">The search filter</param>
	/// <returns>The search results</returns>
	[HttpPost, Route("list/search")]
	[ProducesPaged<MangaBoxType<MbList>>, ProducesError(404), ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Search([FromBody] ListSearchFilter filter) => Box(async () =>
	{
		if (filter.Page <= 0)
			return Boxed.Bad("Page must be greater than 0.");
		if (filter.Size <= 0 || filter.Size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		filter.ProfileId = this.GetProfileId();
		var results = await _db.List.Search(filter);
		return Boxed.Ok(results.Pages, results.Count, results.Results);
	});

	/// <summary>
	/// Searches for lists based on the given filter
	/// </summary>
	/// <param name="filter">The filter to search for</param>
	/// <returns>The paginated search results</returns>
	[HttpGet, Route("list")]
	[ProducesPaged<MangaBoxType<MbList>>, ProducesError(400)]
	public Task<IActionResult> SearchQuery([FromQuery] ListSearchFilter filter) => Search(filter);

	/// <summary>
	/// Imports a list from MD
	/// </summary>
	/// <param name="request">The request to import the list</param>
	/// <param name="token">The token to cancel the request</param>
	/// <returns>The response</returns>
	[HttpPost, Route("list/import/md")]
	[ProducesBox<MbListImportResponse>, ProducesError(404), ProducesError(500), ProducesError(401)]
	public Task<IActionResult> ImportMd([FromBody] MbList.ListImportMD request, CancellationToken token) => Box(() =>
	{
		return _list.Import(request, this.GetProfileId(), token);
	});
}
