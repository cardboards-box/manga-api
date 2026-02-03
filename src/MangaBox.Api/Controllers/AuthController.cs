namespace MangaBox.Api.Controllers;

using Utilities.Auth;

/// <summary>
/// A service for interacting with auth endpoints
/// </summary>
public class AuthController(
	IDbService _db,
	IOAuthService _oauth,
	ILogger<AuthController> logger) : BaseController(logger)
{
#if DEBUG
	private const string? DEFAULT_REDIRECT = "https://localhost:7115/resolve";
#else
	private const string? DEFAULT_REDIRECT = null;
#endif

	/// <summary>
	/// Redirects to the OAuth login URL
	/// </summary>
	/// <param name="redirect">The redirect URL</param>
	/// <returns>The redirect or the error</returns>
	[HttpGet, Route("auth/login"), ProducesError(400)]
	public IActionResult Login([FromQuery] string? redirect = DEFAULT_REDIRECT)
	{
		var url = _oauth.Url(redirect);
		return string.IsNullOrEmpty(url)
			? BadRequest(new { error = "Invalid redirect URL." })
			: Redirect(url);
	}

	/// <summary>
	/// Attempts to resolve the code and log the user in
	/// </summary>
	/// <param name="code">The OAuth platform code</param>
	/// <returns>The response</returns>
	[HttpGet, Route("auth/resolve/{code}")]
	[ProducesBox<AuthResponse>, ProducesError(401)]
	public Task<IActionResult> Resolve([FromRoute] string code) => Box(() =>
	{
		return _oauth.Resolve(code);
	});

#if DEBUG
	/// <summary>
	/// Attempts to resolve the code and log the user in
	/// </summary>
	/// <param name="code">The OAuth platform code</param>
	/// <returns>The response</returns>
	[HttpGet, Route("resolve")]
	[ProducesBox<AuthResponse>, ProducesError(401)]
	public Task<IActionResult> ResolveTemp([FromQuery] string code) => Box(() =>
	{
		return _oauth.Resolve(code);
	});
#endif

	/// <summary>
	/// Fetches the current user's profile
	/// </summary>
	/// <returns>The user's profile</returns>
	[HttpGet, Route("auth/me")]
	[ProducesBox<MbProfile>, ProducesError(401)]
	public Task<IActionResult> Me() => Box(async () =>
	{
		var id = this.GetProfileId();
		if (!id.HasValue) return Boxed.Unauthorized("Not logged in.");

		var profile = await _db.Profile.Fetch(id.Value);
		if (profile == null) return Boxed.Unauthorized("Not logged in.");

		return Boxed.Ok(profile);
	});
}
