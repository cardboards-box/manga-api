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
	/// <returns>The auth response</returns>
	Task<Boxed> Resolve(string code);

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
	/// <returns>The token and the profile</returns>
	Task<AuthResponse> GetToken(MbProfile profile);
}

internal class OAuthService(
	IDbService _db,
	IApiService _api,
	IJwtTokenService _token,
	IConfiguration _config) : IOAuthService
{
	public string AppId => field ??= _config["OAuth:AppId"] ?? throw new ArgumentNullException("OAuth:AppId");
	public string Secret => field ??= _config["OAuth:Secret"] ?? throw new ArgumentNullException("OAuth:Secret");
	public string OAuthUrl => field ??= _config["OAuth:Url"]?.ForceNull()?.TrimEnd('/') ?? "https://auth.index-0.com";
	public string[] ReturnUrls => field ??= _config.GetValue<string[]>("OAuth:ReturnUrls") ?? ["https://localhost:7115/resolve"];

	public async Task<Boxed> Resolve(string code)
	{
		var res = await ResolveCode(code);
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

		var token = await GetToken(profile);
		return Boxed.Ok(token);
	}

	public async Task<AuthResponse> GetToken(MbProfile profile)
	{
		var token = _token.Empty()
			.Add(ClaimTypes.NameIdentifier, profile.Id.ToString())
			.Add(ClaimTypes.Name, profile.Username)
			.Add(ClaimTypes.Email, profile.Email)
			.Add(ClaimTypes.Uri, profile.Avatar ?? string.Empty)
			.Add(ClaimTypes.Authentication, profile.Provider)
			.Add(ClaimTypes.PrimarySid, profile.PlatformId)
			.Add(ClaimTypes.PrimaryGroupSid, profile.ProviderId);

		if (profile.Admin) token.Add(ClaimTypes.Role, "Admin");
		if (profile.CanRead) token.Add(ClaimTypes.Role, "User");

		var jwt = await _token.GenerateToken(token);
		return new(jwt, profile);
	}

	public Task<TokenResponse?> ResolveCode(string code)
	{
		var request = new TokenRequest(code, Secret, AppId);
		return _api.Post<TokenResponse, TokenRequest>($"{OAuthUrl}/api/data", request);
	}

	public string? Url(string? returnUrl)
	{
		if (returnUrl is not null &&
			!ReturnUrls.Contains(returnUrl, StringComparer.InvariantCultureIgnoreCase))
			return null;

		var url = $"{OAuthUrl}/Home/Auth/{AppId}";
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