using System.Diagnostics.CodeAnalysis;

namespace MangaBox.Api.Controllers;

/// <summary>
/// A controller for interacting with log endpoints
/// </summary>
public class LogController(
	IDbService _db,
	ILogger<LogController> logger) : BaseController(logger)
{
	/// <summary>
	/// Validates the request to ensure the user can see logs
	/// </summary>
	/// <param name="resp">The response</param>
	/// <returns>Whether or not the user can see logs</returns>
	[NonAction]
	public bool Validate([NotNullWhen(false)] out Boxed? resp)
	{
		resp = null;
		if (!this.IsAdmin())
		{
			resp = Boxed.NotFound(nameof(MbLog), "Not authorized");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Fetches the log by the given ID
	/// </summary>
	/// <param name="id">The ID of the log</param>
	/// <returns>The log</returns>
	[HttpGet, Route("log/{id}")]
	[ProducesBox<MbLog>, ProducesError(404), ProducesError(400)]
	public Task<IActionResult> Fetch([FromRoute] string id) => Box(async () =>
	{
		if (!Guid.TryParse(id, out var lid))
			return Boxed.Bad("ID isn't valid");

		if (!Validate(out var resp))
			return resp;

		var log = await _db.Log.Fetch(lid);
		if (log is null)
			return Boxed.NotFound(nameof(MbLog), "Log not found");

		return Boxed.Ok(log);
	});

	/// <summary>
	/// Searches for logs
	/// </summary>
	/// <param name="filter">The filter to search with</param>
	/// <returns>The paginated search results</returns>
	[HttpPost, Route("log")]
	[ProducesPaged<MbLog>, ProducesError(404), ProducesError(400)]
	public Task<IActionResult> Search([FromBody] LogSearchFilter filter) => Box(async () =>
	{
		if (filter.Page <= 0)
			return Boxed.Bad("Page must be greater than 0.");
		if (filter.Size <= 0 || filter.Size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");
		if (!Validate(out var resp))
			return resp;
		filter.ProfileId = this.GetProfileId();

		var results = await _db.Log.Search(filter);
		return Boxed.Ok(results.Pages, results.Count, results.Results);
	});

	/// <summary>
	/// Searches for logs
	/// </summary>
	/// <param name="filter">The filter to search with</param>
	/// <returns>The paginated search results</returns>
	[HttpGet, Route("log")]
	[ProducesPaged<MbLog>, ProducesError(404), ProducesError(400)]
	public Task<IActionResult> SearchQuery([FromQuery] LogSearchFilter filter) => Search(filter);
}
