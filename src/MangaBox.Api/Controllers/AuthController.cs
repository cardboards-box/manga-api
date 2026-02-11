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
	private const string DEFAULT_REDIRECT = "https://localhost:7115/auth/resolve";
#else
	private const string DEFAULT_REDIRECT = "https://mangabox.app/auth";
#endif

	/// <summary>
	/// Redirects to the OAuth login URL
	/// </summary>
	/// <param name="provider">The OAuth provider platform</param>
	/// <param name="redirect">The redirect URL</param>
	/// <returns>The redirect or the error</returns>
	[HttpGet, Route("auth/login/{provider}"), ProducesError(400)]
	public async Task<IActionResult> Login([FromRoute] string provider, [FromQuery] string redirect = DEFAULT_REDIRECT)
	{
		var (error, url) = await _oauth.Start(redirect, provider);
		return !string.IsNullOrEmpty(error) || string.IsNullOrEmpty(url)
			? BadRequest(new { error = error ?? "Invalid redirect URL." })
			: Redirect(url);
	}

	/// <summary>
	/// Attempts to resolve the code and log the user in
	/// </summary>
	/// <param name="provider">The OAuth provider platform</param>
	/// <param name="state">The OAuth state</param>
	/// <param name="code">The OAuth platform code</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	[HttpGet, Route("auth/resolve/{provider}"), ProducesError(400)]
	public async Task<IActionResult> Resolve([FromRoute] string provider, [FromQuery] string state, [FromQuery] string code, CancellationToken token)
	{
		var (error, url) = await _oauth.HandleCallBack(provider, code, state, token);
		if (!string.IsNullOrEmpty(error) || url is null)
			return BadRequest(new { error = error ?? "Failed to resolve OAuth callback." });

		return Redirect(url);
	}

	/// <summary>
	/// Attempts to resolve the code and log the user in
	/// </summary>
	/// <param name="code">The resolve code</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The auth response</returns>
	[HttpGet, Route("auth/resolve")]
	[ProducesBox<AuthResponse>, ProducesError(401)]
	public Task<IActionResult> ResolveCode([FromQuery] string code, CancellationToken token) => Box(async () =>
	{
		var (error, auth) = await _oauth.ResolveCode(code, token);
		if (!string.IsNullOrEmpty(error) || auth is null)
			return Boxed.Exception(error ?? "Auth was null");
		return Boxed.Ok(auth);
	});

	/// <summary>
	/// Fetches the current user's profile
	/// </summary>
	/// <returns>The user's profile</returns>
	[HttpGet, Route("auth/me")]
	[ProducesBox<MbProfile>, ProducesError(401)]
	public Task<IActionResult> Me() => Box(async () =>
	{
		var id = this.GetBaseProfileId();
		if (!id.HasValue) return Boxed.Unauthorized("Not logged in.");

		var profile = await _db.Profile.Fetch(id.Value);
		if (profile == null) return Boxed.Unauthorized("Not logged in.");

		return Boxed.Ok(profile);
	});

	/// <summary>
	/// Update the settings blob for the user's profile
	/// </summary>
	/// <param name="settings">The settings blob</param>
	/// <returns>The user's profile</returns>
	[HttpPut, Route("auth/settings")]
	[ProducesBox<MbProfile>, ProducesError(401), ProducesError(404)]
	public Task<IActionResult> Settings([FromBody] SetSettings settings) => Box(async () =>
	{
		var id = this.GetBaseProfileId();
		if (!id.HasValue) return Boxed.Unauthorized("Not logged in.");

		var profile = await _db.Profile.Settings(id.Value, settings.Settings);
		if (profile is null) return Boxed.NotFound("Profile was not found");

		return Boxed.Ok(profile);
	});

	/// <summary>
	/// The request to set the settings blob
	/// </summary>
	/// <param name="Settings">The settings blob</param>
	public record class SetSettings(
		[property: JsonPropertyName("settings")] string? Settings);
}
