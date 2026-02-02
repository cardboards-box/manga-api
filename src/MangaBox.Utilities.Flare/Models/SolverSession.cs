using System.Collections.Specialized;

namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// Represents a solver session
/// </summary>
public class SolverSession : IAsyncDisposable, IFlareSolverBase
{
    private readonly IFlareSolverApiService _api;
    private readonly string _sessionId;

    internal SolverSession(IFlareSolverApiService api, string sessionId)
    {
        _api = api;
        _sessionId = sessionId;
    }

    /// <inheritdoc />
	public Task<SolverResponse?> Get(string url, SolverCookie[]? cookies = null, SolverProxy? proxy = null, int? timeout = null)
    {
        return _api.Get(url, cookies: cookies, proxy: proxy, sessionId: _sessionId, maxTimeout: timeout);
    }

	/// <inheritdoc />
	public Task<SolverResponse?> Post(string url, NameValueCollection data, SolverCookie[]? cookies = null, SolverProxy? proxy = null, int? timeout = null)
    {
        return _api.Post(url, data, cookies: cookies, proxy: proxy, sessionId: _sessionId, maxTimeout: timeout);
    }

	/// <inheritdoc />
	public Task<SolverResponse?> Post(string url, Dictionary<string, string> data, SolverCookie[]? cookies = null, SolverProxy? proxy = null, int? timeout = null)
    {
        var collection = new NameValueCollection();
        foreach (var (key, value) in data)
        {
            collection.Add(key, value);
        }
        return Post(url, collection, cookies: cookies, proxy: proxy, timeout: timeout);
    }

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
    {
        await _api.SessionDestroy(_sessionId);
        GC.SuppressFinalize(this);
    }
}
