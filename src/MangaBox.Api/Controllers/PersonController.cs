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
}
