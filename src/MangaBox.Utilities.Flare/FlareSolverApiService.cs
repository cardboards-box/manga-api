using System.Collections.Specialized;

namespace MangaBox.Utilities.Flare;

using Models;

/// <summary>
/// A service for interacting with the flare solver API
/// </summary>
/// <remarks>You probably don't want this class, prefer: <see cref="IFlareSolverService"/></remarks>
internal interface IFlareSolverApiService
{
    /// <summary>
    /// Fetches data from the flare solver API
    /// </summary>
    /// <param name="url">The URL to use</param>
    /// <param name="sessionId">The session ID to use</param>
    /// <param name="cookies">The cookies to include</param>
    /// <param name="proxy">The proxy to use</param>
    /// <param name="returnOnlyCookies">Whether to return only cookies</param>
    /// <param name="maxTimeout">The maximum timeout</param>
    /// <returns>The solver response</returns>
    Task<SolverResponse?> Get(string url,
        string? sessionId = null,
        SolverCookie[]? cookies = null,
        SolverProxy? proxy = null,
        bool returnOnlyCookies = false,
        int? maxTimeout = null);

	/// <summary>
	/// Fetches data from the flare solver API with a POST
	/// </summary>
	/// <param name="url">The URL to use</param>
    /// <param name="parameters">The body parameters</param>
	/// <param name="sessionId">The session ID to use</param>
	/// <param name="cookies">The cookies to include</param>
	/// <param name="proxy">The proxy to use</param>
	/// <param name="returnOnlyCookies">Whether to return only cookies</param>
	/// <param name="maxTimeout">The maximum timeout</param>
	/// <returns>The solver response</returns>
	Task<SolverResponse?> Post(string url,
        NameValueCollection parameters,
        string? sessionId = null,
        SolverCookie[]? cookies = null,
        SolverProxy? proxy = null,
        bool returnOnlyCookies = false,
        int? maxTimeout = null);

    /// <summary>
    /// Fetches a list of all of the active sessions
    /// </summary>
    /// <returns>The list of active sessions</returns>
    Task<SolverSessionList?> SessionList();
    
    /// <summary>
    /// Creates a new session
    /// </summary>
    /// <param name="sessionId">The session ID to use</param>
    /// <param name="proxy">The proxy to use</param>
    /// <returns>The created session</returns>
    Task<SolverSessionCreate?> SessionCreate(string? sessionId = null, SolverProxy? proxy = null);

    /// <summary>
    /// Destroys the given session
    /// </summary>
    /// <param name="sessionId">The session ID to use</param>
    /// <returns>Whether or not the session was destroyed</returns>
    Task<SolverSessionDestroy?> SessionDestroy(string sessionId);
}

internal class FlareSolverApiService(
    IApiService _api,
    IConfiguration _config) : IFlareSolverApiService
{
    private string? _serverUrl;
    private const int DEFAULT_TIMEOUT = 60_000;

    public string SolverUrl => _config["FlareSolver:Url"]
        ?? throw new InvalidOperationException("FlareSolver:Url is not set in the configuration.");

    public string Version => _config["FlareSolver:Version"] ?? "v1";

    public string ServerUrl => _serverUrl ??= $"{SolverUrl?.TrimEnd('/')}/{Version.Trim('/')}";

    public Task<SolverResponse?> Get(string url,
        string? sessionId = null,
        SolverCookie[]? cookies = null,
        SolverProxy? proxy = null,
        bool returnOnlyCookies = false,
        int? maxTimeout = null)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_GET,
            Url = url,
            SessionId = sessionId,
            Cookies = cookies,
            Proxy = proxy,
            MaxTimeout = maxTimeout ?? DEFAULT_TIMEOUT,
            ReturnOnlyCookies = returnOnlyCookies
        };
        return _api.Post<SolverResponse, SolverRequest>(ServerUrl, request);
    }

    public Task<SolverResponse?> Post(string url, 
        NameValueCollection parameters, 
        string? sessionId = null, 
        SolverCookie[]? cookies = null, 
        SolverProxy? proxy = null, 
        bool returnOnlyCookies = false, 
        int? maxTimeout = null)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_POST,
            PostData = parameters,
            Url = url,
            SessionId = sessionId,
            Cookies = cookies,
            Proxy = proxy,
            MaxTimeout = maxTimeout ?? DEFAULT_TIMEOUT,
            ReturnOnlyCookies = returnOnlyCookies
        };
        return _api.Post<SolverResponse, SolverRequest>(ServerUrl, request);
    }

    public Task<SolverSessionCreate?> SessionCreate(string? sessionId = null, SolverProxy? proxy = null)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_CREATE,
            SessionId = sessionId,
            Proxy = proxy
        };
        return _api.Post<SolverSessionCreate, SolverRequest>(ServerUrl, request);
    }

    public Task<SolverSessionDestroy?> SessionDestroy(string sessionId)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_DESTROY,
            SessionId = sessionId,
        };
        return _api.Post<SolverSessionDestroy, SolverRequest>(ServerUrl, request);
    }

    public Task<SolverSessionList?> SessionList()
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_LIST
        };
        return _api.Post<SolverSessionList, SolverRequest>(ServerUrl, request);
    }
}
