using CardboardBox.Redis;
using Flurl;
using System.IO.Enumeration;
using System.Security.Claims;
using System.Security.Cryptography;

namespace MangaBox.Utilities.Auth;

using Database;
using Jwt;
using Models;
using Models.Composites;

/// <summary>
/// The service for interfacing with the OAuth platform
/// </summary>
public interface IOAuthService
{
	/// <summary>
	/// Gets the auth URL for the given provider and return URL
	/// </summary>
	/// <param name="returnUrl">The return URL</param>
	/// <param name="provider">The provider</param>
	/// <returns>The error and auth url</returns>
	Task<(string? error, string? url)> Start(string returnUrl, string provider);

	/// <summary>
	/// Handles the OAuth callback
	/// </summary>
	/// <param name="provider">The provider </param>
	/// <param name="code">The authorization code</param>
	/// <param name="stateId">The state identifier</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The error and return URL</returns>
	Task<(string? error, string? url)> HandleCallBack(string provider, string code, string stateId, CancellationToken token);

	/// <summary>
	/// Resolves an Auth code
	/// </summary>
	/// <param name="code">The auth code</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The error and token</returns>
	Task<(string? error, AuthResponse? reps)> ResolveCode(string code, CancellationToken token);

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
	IOptions<AuthOptions> _config,
	IRedisService _redis,
	IJwtTokenService _token,
	ILogger<OAuthService> _logger,
	IEnumerable<IAuthProviderService> _providers) : IOAuthService
{
	public const string STATE_KEY = "oauth:state:{0}";
	public const string PID_KEY = "oauth:pid:{0}";
	public const int CODE_BYTES = 32;

	public TimeSpan StateTTL => TimeSpan.FromMinutes(_config.Value.TTLMinutes);

	public TimeSpan PidTTL => TimeSpan.FromMinutes(_config.Value.TTLMinutes);

	public bool ValidReturnUrl(string returnUrl)
	{
		foreach(var url in _config.Value.ReturnUrls)
		{
			var pattern = url.AsSpan();
			if (FileSystemName.MatchesWin32Expression(pattern, returnUrl, true))
				return true;
		}

		return false;
	}

	public static string GetStateKey(Guid stateId) => string.Format(STATE_KEY, stateId);

	public static string GetPidKey(string pid) => string.Format(PID_KEY, pid);

	public static string GenerateCode()
	{
		var buf = RandomNumberGenerator.GetBytes(CODE_BYTES);
		return Convert.ToBase64String(buf)
			.Replace("+", "-")
			.Replace("/", "_")
			.TrimEnd('=')
			.SHA256Hash();
	}

	public string CallBackUrl(string provider) => $"{_config.Value.AppUrl.TrimEnd('/')}/auth/resolve/{provider}";

	public async Task<(string? error, string? url)> Start(string returnUrl, string provider)
	{
		if (!ValidReturnUrl(returnUrl))
			return ("Invalid return URL", null);

		var prov = _providers.FirstOrDefault(p => p.Name.EqualsIc(provider));
		if (prov is null)
			return ("Invalid provider", null);

		var stateId = Guid.NewGuid();
		var state = new OAuthState(stateId, prov.Name, returnUrl);
		var key = GetStateKey(stateId);
		
		if (!await _redis.Set(key, state, StateTTL))
			return ("Failed to store state", null);

		return (null, prov.AuthUrl(stateId, CallBackUrl(prov.Name)));
	}

	public async Task<(string? error, string? url)> HandleCallBack(string provider, string code, string stateId, CancellationToken token)
	{
		if (!Guid.TryParse(stateId, out var stateGuid)) return ("Invalid State (1)", null);

		var key = GetStateKey(stateGuid);
		var state = await _redis.Get<OAuthState>(key);
		if (state is null) return ("Invalid State (2)", null);

		await _redis.Delete(key);

		var prov = _providers.FirstOrDefault(p => p.Name.EqualsIc(provider));
		if (prov is null) return ("Invalid Provider", null);

		var callback = CallBackUrl(prov.Name);
		try
		{
			var accessToken = await prov.GetAccessToken(code, callback, token);
			var user = await prov.GetUser(accessToken, token);

			var pid = await _db.Profile.Upsert(new()
			{
				Avatar = user.Avatar,
				Email = user.Email,
				Username = user.Username,
				Provider = user.Provider,
				ProviderId = user.ProviderId,
				PlatformId = $"{user.Provider}:{user.ProviderId}",
			});
			code = GenerateCode();
			key = GetPidKey(code);
			await _redis.Set(key, pid.ToString(), PidTTL);

			return (null, state.ReturnUrl
				.AppendQueryParam("code", code));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while resolving access token");
			return ("Failed to resolve profile", null);
		}
	}

	public async Task<(string? error, AuthResponse? reps)> ResolveCode(string code, CancellationToken token)
	{
		var key = GetPidKey(code);
		var pid = await _redis.Get(key);
		if (pid is null) return ("Invalid State (1)", null);

		await _redis.Delete(key);

		if (!Guid.TryParse(pid, out var pidGuid)) 
			return ("Invalid State (2)", null);

		await _redis.Delete(key);
		var profile = await _db.Profile.Fetch(pidGuid);
		if (profile is null) return ("Invalid Profile", null);
		return (null, await GetToken(profile, token));
	}

	public async Task<AuthResponse> GetToken(MbProfile profile, CancellationToken cancel)
	{
		var token = _token.Empty()
			.Add(ClaimTypes.NameIdentifier, profile.Id.ToString())
			.Add(ClaimTypes.Gender, GenerateCode());

		if (profile.Admin) token.Add(ClaimTypes.Role, Constants.ROLE_ADMIN);
		if (profile.CanRead) token.Add(ClaimTypes.Role, Constants.ROLE_USER);

		var jwt = await _token.GenerateToken(token, cancel);
		return new(jwt, profile);
	}

	public record class OAuthState(
		[property: JsonPropertyName("id")] Guid Id, 
		[property: JsonPropertyName("provider")] string Provider,
		[property: JsonPropertyName("returnUrl")] string ReturnUrl);
}
