using System.Collections.Specialized;

namespace MangaBox.Utilities.Flare;

using Models;

/// <summary>
/// A service for bypassing Cloudflare protections
/// </summary>
public interface IFlareSolverBase
{
	/// <summary>
	/// Fetches a URL using FlareSolverr
	/// </summary>
	/// <param name="url">The URL to fetch</param>
	/// <param name="cookies">The cookies for the request</param>
	/// <param name="proxy">Optional proxy to use for the session</param>
	/// <param name="timeout">The timeout to use</param>
	/// <param name="disableMedia">Whether to disable media</param>
	/// <param name="returnScreenshot">Whether or not to return a screenshot of the page</param>
	/// <param name="waitInSeconds">The number of seconds to wait after the page loads</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response from flare solver</returns>
	Task<SolverResponse?> Get(
        string url, 
        SolverCookie[]? cookies = null, 
        SolverProxy? proxy = null, 
        int? timeout = null,
        bool returnScreenshot = false,
        bool disableMedia = false,
        double? waitInSeconds = null,
        CancellationToken token = default);

	/// <summary>
	/// Fetches a URL using FlareSolverr with POST data
	/// </summary>
	/// <param name="url">The URL to fetch</param>
	/// <param name="data">The data for the request</param>
	/// <param name="cookies">The cookies for the request</param>
	/// <param name="proxy">Optional proxy to use for the session</param>
	/// <param name="timeout">The timeout to use</param>
	/// <param name="disableMedia">Whether to disable media</param>
	/// <param name="returnScreenshot">Whether or not to return a screenshot of the page</param>
	/// <param name="waitInSeconds">The number of seconds to wait after the page loads</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response from flare solver</returns>
	Task<SolverResponse?> Post(
        string url, 
        NameValueCollection data, 
        SolverCookie[]? cookies = null, 
        SolverProxy? proxy = null, 
        int? timeout = null,
		bool returnScreenshot = false,
		bool disableMedia = false,
		double? waitInSeconds = null,
		CancellationToken token = default);

	/// <summary>
	/// Fetches a URL using FlareSolverr with POST data
	/// </summary>
	/// <param name="url">The URL to fetch</param>
	/// <param name="data">The data for the request</param>
	/// <param name="cookies">The cookies for the request</param>
	/// <param name="proxy">Optional proxy to use for the session</param>
	/// <param name="timeout">The timeout to use</param>
	/// <param name="disableMedia">Whether to disable media</param>
	/// <param name="returnScreenshot">Whether or not to return a screenshot of the page</param>
	/// <param name="waitInSeconds">The number of seconds to wait after the page loads</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response from flare solver</returns>
	Task<SolverResponse?> Post(
        string url, 
        Dictionary<string, string> data, 
        SolverCookie[]? cookies = null, 
        SolverProxy? proxy = null, 
        int? timeout = null,
		bool returnScreenshot = false,
		bool disableMedia = false,
		double? waitInSeconds = null,
		CancellationToken token = default);
}

/// <summary>
/// A service for bypassing Cloudflare protections
/// </summary>
public interface IFlareSolverService : IFlareSolverBase
{
	/// <summary>
	/// Create a flare session
	/// </summary>
	/// <param name="proxy">Optional proxy to use for the session</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The session that was created</returns>
	Task<SolverSession> CreateSession(SolverProxy? proxy, CancellationToken token);

    /// <summary>
    /// Clears all of the flare sessions
    /// </summary>
    Task ClearSessions(CancellationToken token);

    /// <summary>
    /// Creates and returns a new instance of the FlareSolver.
    /// </summary>
    /// <returns>A new instance of <see cref="FlareSolverInstance"/> that can be used to manage flare solving operations.</returns>
    FlareSolverInstance Limiter();
}

internal class FlareSolverService(
    IFlareSolverApiService _api,
    ILogger<FlareSolverService> _logger) : IFlareSolverService
{
    public async Task ClearSessions(CancellationToken token)
    {
        var sessions = await _api.SessionList(token);
        if (sessions is null || sessions.SessionIds is null || sessions.SessionIds.Length == 0)
            return;

        foreach (var sessionId in sessions.SessionIds)
            await _api.SessionDestroy(sessionId, token);
    }

    public async Task<SolverSession> CreateSession(SolverProxy? proxy, CancellationToken token)
    {
        var session = await _api.SessionCreate(null, proxy, token)
            ?? throw new Exception("Failed to create session");

        return new SolverSession(_api, session.SessionId);
    }

    public Task<SolverResponse?> Get(
        string url, 
        SolverCookie[]? cookies, 
        SolverProxy? proxy, 
        int? timeout,
		bool returnScreenshot,
		bool disableMedia,
		double? waitInSeconds,
		CancellationToken token)
    {
        return _api.Get(url, null, cookies, proxy, false, timeout, disableMedia, returnScreenshot, waitInSeconds, token);
    }

    public Task<SolverResponse?> Post(
        string url, 
        NameValueCollection data, 
        SolverCookie[]? cookies, 
        SolverProxy? proxy, 
        int? timeout,
		bool returnScreenshot,
		bool disableMedia,
		double? waitInSeconds,
		CancellationToken token)
    {
        return _api.Post(url, data, null, cookies, proxy, false, timeout, disableMedia, returnScreenshot, waitInSeconds, token);
    }

    public Task<SolverResponse?> Post(
        string url, 
        Dictionary<string, string> data, 
        SolverCookie[]? cookies, 
        SolverProxy? proxy, 
        int? timeout,
		bool returnScreenshot,
		bool disableMedia,
		double? waitInSeconds,
		CancellationToken token)
    {
        var collection = new NameValueCollection();
        foreach (var (key, value) in data)
        {
            collection.Add(key, value);
        }
        return Post(url, collection, cookies, proxy, timeout, returnScreenshot, disableMedia, waitInSeconds, token);
    }

    public FlareSolverInstance Limiter() => new(this, _logger);
}
