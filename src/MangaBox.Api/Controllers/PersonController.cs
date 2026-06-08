namespace MangaBox.Api.Controllers;

/// <summary>
/// A service for interacting with people
/// </summary>
public class PersonController(
	IDbService _db,
	ILogger<PersonController> logger) : BaseController(logger) 
{
	/// <summary>
	/// Searches for people by name
	/// </summary>
	/// <param name="query">The search query</param>
	/// <param name="page">The page number</param>
	/// <param name="size">The number of results per page</param>
	/// <param name="asc">Whether to sort in ascending order</param>
	/// <returns>The paginated search results</returns>
	[HttpGet, Route("person")]
	[ProducesPaged<MbPerson>, ProducesError(400)]
	public Task<IActionResult> Search(
		[FromQuery] string? query,
		[FromQuery] int page = 1,
		[FromQuery] int size = 20,
		[FromQuery] bool asc = true) => Box(async () => 
	{
		if (page <= 0)
			return Boxed.Bad("Page must be greater than 0.");
		if (size <= 0 || size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		var result = await _db.Person.Search(query, page, size, asc);
		return Boxed.Ok(result.Pages, result.Count, result.Results);
	});

	/// <summary>
	/// Gets multiple people by their IDs
	/// </summary>
	/// <param name="ids">The IDs of the people</param>
	/// <returns>The people with the specified IDs</returns>
	[HttpPost, Route("person")]
	[ProducesArray<MbPerson>, ProducesError(400)]
	public Task<IActionResult> GetByIds([FromBody] string[] ids) => Box(async () =>
	{
		var guids = ids
			.Select(t => Guid.TryParse(t, out var id) ? id : (Guid?)null)
			.ToArray();
		if (guids.Any(t => t is null))
			return Boxed.Bad("One or more invalid person IDs.");

		var gids = guids.Where(t => t.HasValue).Select(t => t!.Value).ToArray();
		var people = await _db.Person.Get(gids);
		return Boxed.Ok(people);
	});

	/// <summary>
	/// Gets a person by their ID
	/// </summary>
	/// <param name="id">The ID of the person</param>
	/// <returns>The person with the specified ID</returns>
	[HttpGet, Route("person/{id}")]
	[ProducesBox<MbPerson>, ProducesError(400), ProducesError(404)]
	public Task<IActionResult> Get([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var guid))
			return Boxed.Bad($"Invalid person ID: {id}");
		var person = await _db.Person.Fetch(guid);
		if (person is null)
			return Boxed.NotFound($"Person with ID {id} not found.");
		return Boxed.Ok(person);
	});
}
