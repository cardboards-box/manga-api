using System.Security.Claims;

namespace MangaBox.Utilities.Auth;

using Database;
using Jwt;
using Models;

/// <summary>
/// A service for interfacing with the OAuth platform
/// </summary>
public interface IOAuthService
{
	/// <summary>
	/// Resolves the code and returns the auth response
	/// </summary>
	/// <param name="code">The OAuth code</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The auth response</returns>
	Task<Boxed> Resolve(string code, CancellationToken token);

	/// <summary>
	/// Gets the authentication URL for the OAuth platform
	/// </summary>
	/// <param name="returnUrl">The return URL to use</param>
	/// <returns>The auth URL</returns>
	string? Url(string? returnUrl);

	/// <summary>
	/// Gets the JWT token for the given profile
	/// </summary>
	/// <param name="profile">The profile to get the token for</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The token and the profile</returns>
	Task<AuthResponse> GetToken(MbProfile profile, CancellationToken token);
}

internal class OAuthService(
	IDbService _db,
	IApiService _api,
	IJwtTokenService _token,
	IOptions<OAuthOptions> _config) : IOAuthService
{
	public async Task<Boxed> Resolve(string code, CancellationToken cancel)
	{
		var res = await ResolveCode(code, cancel);
		if (res is null || !string.IsNullOrEmpty(res.Error))
			return Boxed.Unauthorized(res?.Error ?? "Login Failed (1)");
		if (res.User is null) return Boxed.Unauthorized("Login Failed (2)");
		if (res.App is null) return Boxed.Unauthorized("Login Failed (3)");

		var id = await _db.Profile.Upsert(new()
		{
			Avatar = res.User.Avatar,
			Email = res.User.Email,
			PlatformId = res.User.Id,
			Username = res.User.Nickname,
			Provider = res.User.Provider,
			ProviderId = res.User.ProviderId,
		});

		var profile = await _db.Profile.Fetch(id);
		if (profile is null) 
			return Boxed.Unauthorized("Login Failed (4)");

		var token = await GetToken(profile, cancel);
		return Boxed.Ok(token);
	}

	public async Task<AuthResponse> GetToken(MbProfile profile, CancellationToken cancel)
	{
		var token = _token.Empty()
			.Add(ClaimTypes.NameIdentifier, profile.Id.ToString())
			.Add(ClaimTypes.Name, profile.Username)
			.Add(ClaimTypes.Email, profile.Email)
			.Add(ClaimTypes.Uri, profile.Avatar ?? string.Empty)
			.Add(ClaimTypes.Authentication, profile.Provider)
			.Add(ClaimTypes.PrimarySid, profile.PlatformId)
			.Add(ClaimTypes.PrimaryGroupSid, profile.ProviderId);

		if (profile.Admin) token.Add(ClaimTypes.Role, Constants.ROLE_ADMIN);
		if (profile.CanRead) token.Add(ClaimTypes.Role, Constants.ROLE_USER);

		var jwt = await _token.GenerateToken(token, cancel);
		return new(jwt, profile);
	}

	public Task<TokenResponse?> ResolveCode(string code, CancellationToken token)
	{
		var request = new TokenRequest(code, _config.Value.Secret, _config.Value.AppId);
		return _api.Post<TokenResponse, TokenRequest>($"{_config.Value.Url}/api/data", request, token: token);
	}

	public string? Url(string? returnUrl)
	{
		if (returnUrl is not null &&
			!_config.Value.ReturnUrls.Contains(returnUrl, StringComparer.InvariantCultureIgnoreCase))
			return null;

		var url = $"{_config.Value.Url}/Home/Auth/{_config.Value.AppId}";
		if (returnUrl is not null)
			url += $"?redirect={Uri.EscapeDataString(returnUrl)}";

		return url;
	}
}

/// <summary>
/// The response to an auth request
/// </summary>
/// <param name="Token">The JWT token</param>
/// <param name="Profile">The user's profile</param>
public record class AuthResponse(
	[property: JsonPropertyName("token")] string Token,
	[property: JsonPropertyName("profile")] MbProfile Profile);