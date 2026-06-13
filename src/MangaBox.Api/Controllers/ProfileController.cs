namespace MangaBox.Api.Controllers;

using Models.Composites.Filters;

/// <summary>
/// A service for administrating profiles
/// </summary>
public class ProfileController(
	IDbService _db,
	ILogger<ProfileController> logger) : BaseController(logger)
{
	/// <summary>
	/// Searches profiles by the given filter
	/// </summary>
	/// <param name="filter">The filter to search by</param>
	/// <returns>The paginated profile results</returns>
	[HttpPost, Route("profile/search")]
	[ProducesPaged<MbProfile>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> Search([FromBody] ProfileSearchFilter filter) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");
		if (filter.Page <= 0)
			return Boxed.Bad("Page must be greater than 0.");
		if (filter.Size <= 0 || filter.Size > 100)
			return Boxed.Bad("Size must be between 1 and 100.");

		var results = await _db.Profile.Search(filter);
		return Boxed.Ok(results.Pages, results.Count, results.Results);
	});

	/// <summary>
	/// Searches profiles by the given filter
	/// </summary>
	/// <param name="filter">The filter to search by</param>
	/// <returns>The paginated profile results</returns>
	[HttpGet, Route("profile/search")]
	[ProducesPaged<MbProfile>, ProducesError(400), ProducesError(401)]
	public Task<IActionResult> SearchQuery([FromQuery] ProfileSearchFilter filter) => Search(filter);

	/// <summary>
	/// Gets all profile providers
	/// </summary>
	/// <returns>The profile providers</returns>
	[HttpGet, Route("profile/providers")]
	[ProducesArray<string>, ProducesError(401)]
	public Task<IActionResult> Providers() => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");

		var providers = await _db.Profile.Providers();
		return Boxed.Ok(providers);
	});

	/// <summary>
	/// Updates whether or not a profile is approved to read
	/// </summary>
	/// <param name="id">The profile ID</param>
	/// <param name="request">The request body</param>
	/// <returns>The updated profile</returns>
	[HttpPut, Route("profile/{id}/can-read")]
	[ProducesBox<MbProfile>, ProducesError(400), ProducesError(401), ProducesError(404)]
	public Task<IActionResult> CanRead([FromRoute] string id, [FromBody] UpdateBooleanRequest request) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");
		if (!Guid.TryParse(id, out var profileId))
			return Boxed.Bad("Profile ID is not a valid GUID.");

		var profile = await _db.Profile.CanRead(profileId, request.Value);
		if (profile is null)
			return Boxed.NotFound(nameof(MbProfile), "Profile was not found.");

		return Boxed.Ok(profile);
	});

	/// <summary>
	/// Updates whether or not a profile is an administrator
	/// </summary>
	/// <param name="id">The profile ID</param>
	/// <param name="request">The request body</param>
	/// <returns>The updated profile</returns>
	[HttpPut, Route("profile/{id}/admin")]
	[ProducesBox<MbProfile>, ProducesError(400), ProducesError(401), ProducesError(404)]
	public Task<IActionResult> Admin([FromRoute] string id, [FromBody] UpdateBooleanRequest request) => Box(async () =>
	{
		if (!this.IsAdmin())
			return Boxed.Unauthorized("You cannot perform this action");
		if (!Guid.TryParse(id, out var profileId))
			return Boxed.Bad("Profile ID is not a valid GUID.");

		var profile = await _db.Profile.Admin(profileId, request.Value);
		if (profile is null)
			return Boxed.NotFound(nameof(MbProfile), "Profile was not found.");

		return Boxed.Ok(profile);
	});

	/// <summary>
	/// A request to update a boolean profile field
	/// </summary>
	/// <param name="Value">The value to set</param>
	public record class UpdateBooleanRequest(
		[property: JsonPropertyName("value")] bool Value);
}
