using System.Security.Claims;
using CardboardBox.Redis;

namespace MangaBox.Services;

using Utilities.Auth;

/// <summary>
/// A service for managing API keys
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Clears the cached logins for the given profile's API keys
    /// </summary>
    /// <param name="pid">The ID of the profile</param>
    Task ClearCache(Guid pid);

    /// <summary>
    /// Validates an API key and returns the claims if valid
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>The status and the claims</returns>
    Task<(bool success, Claim[] claims)> Validate(string apiKey);
}

internal class ApiKeyService(
    IDbService _db,
    IRedisService _redis,
    IConfiguration _config,
    IOAuthService _auth) : IApiKeyService
{
    private const string CACHE_KEY_PREFIX = "apikey:{0}";
    private const string SETTINGS_KEY = "OAuth:ApiKeyCache:TTL";

    private TimeSpan? _cacheTTL = null;

    private TimeSpan CacheTTL => _cacheTTL ??= TimeSpan.FromSeconds(
        double.TryParse(_config[SETTINGS_KEY], out var ttl) ? ttl : 300);

    public static string GetKey(string apiKey) => string.Format(CACHE_KEY_PREFIX, apiKey);

    public Task<MbProfile?> FetchFromCache(string apiKey)
    {
        return _redis.Get<MbProfile>(GetKey(apiKey));
    }

    public Task SetCache(string apiKey, MbProfile profile)
    {
        return _redis.Set(GetKey(apiKey), profile, CacheTTL);
    }

    public async Task ClearCache(Guid pid)
    {
        var keys = await _db.ApiKey.GetByProfile(pid);
        var tasks = keys.Select(k => _redis.Delete(GetKey(k.Key)));
        await Task.WhenAll(tasks);
    }

    public async Task<(bool success, Claim[] claims)> Validate(string apiKey)
    {
        var profile = await FetchFromCache(apiKey);
        if (profile is not null)
            return (true, [.._auth.TokenFromProfile(profile)]);

        var key = await _db.ApiKey.FetchByKey(apiKey);
        if (key is null) return (false, []);

        profile = key.GetItem<MbProfile>();
        if (profile is null) return (false, []);

        await SetCache(apiKey, profile);
        return (true, [.._auth.TokenFromProfile(profile)]);
    }

}
