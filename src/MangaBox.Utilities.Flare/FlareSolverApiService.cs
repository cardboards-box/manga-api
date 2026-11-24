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
    /// <param name="disableMedia">Whether to disable media</param>
    /// <param name="returnScreenshot">Whether or not to return a screenshot of the page</param>
    /// <param name="waitInSeconds">The number of seconds to wait after the page loads</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The solver response</returns>
	Task<SolverResponse?> Get(string url,
        string? sessionId,
        SolverCookie[]? cookies,
        SolverProxy? proxy,
        bool returnOnlyCookies,
        int? maxTimeout,
        bool disableMedia,
        bool returnScreenshot,
		double? waitInSeconds,
        CancellationToken token);

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
	/// <param name="disableMedia">Whether to disable media</param>
	/// <param name="returnScreenshot">Whether or not to return a screenshot of the page</param>
	/// <param name="waitInSeconds">The number of seconds to wait after the page loads</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The solver response</returns>
	Task<SolverResponse?> Post(string url,
        NameValueCollection parameters,
        string? sessionId,
        SolverCookie[]? cookies,
        SolverProxy? proxy,
		bool returnOnlyCookies,
		int? maxTimeout,
		bool disableMedia,
		bool returnScreenshot,
		double? waitInSeconds,
		CancellationToken token);

	/// <summary>
	/// Fetches a list of all of the active sessions
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The list of active sessions</returns>
	Task<SolverSessionList?> SessionList(CancellationToken token);

	/// <summary>
	/// Creates a new session
	/// </summary>
	/// <param name="sessionId">The session ID to use</param>
	/// <param name="proxy">The proxy to use</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The created session</returns>
	Task<SolverSessionCreate?> SessionCreate(string? sessionId, SolverProxy? proxy, CancellationToken token);

	/// <summary>
	/// Destroys the given session
	/// </summary>
	/// <param name="sessionId">The session ID to use</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the session was destroyed</returns>
	Task<SolverSessionDestroy?> SessionDestroy(string sessionId, CancellationToken token);
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
        string? sessionId,
        SolverCookie[]? cookies,
        SolverProxy? proxy,
        bool returnOnlyCookies,
        int? maxTimeout,
		bool disableMedia,
		bool returnScreenshot,
		double? waitInSeconds,
		CancellationToken token)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_GET,
            Url = url,
            SessionId = sessionId,
            Cookies = cookies,
            Proxy = proxy,
            MaxTimeout = maxTimeout ?? DEFAULT_TIMEOUT,
            ReturnOnlyCookies = returnOnlyCookies,
            DisableMedia = disableMedia,
            WaitInSeconds = waitInSeconds,
            ReturnScreenshot = returnScreenshot,
        };
		return Send<SolverResponse>(request, token);
    }

    public Task<SolverResponse?> Post(string url, 
        NameValueCollection parameters, 
        string? sessionId, 
        SolverCookie[]? cookies, 
        SolverProxy? proxy, 
        bool returnOnlyCookies, 
        int? maxTimeout,
		bool disableMedia,
		bool returnScreenshot,
		double? waitInSeconds,
		CancellationToken token)
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
            ReturnOnlyCookies = returnOnlyCookies,
			DisableMedia = disableMedia,
			WaitInSeconds = waitInSeconds,
			ReturnScreenshot = returnScreenshot,
		};
		return Send<SolverResponse>(request, token);
    }

    public Task<SolverSessionCreate?> SessionCreate(string? sessionId, SolverProxy? proxy, CancellationToken token)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_CREATE,
            SessionId = sessionId,
            Proxy = proxy
        };
		return Send<SolverSessionCreate>(request, token);
    }

    public Task<SolverSessionDestroy?> SessionDestroy(string sessionId, CancellationToken token)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_DESTROY,
            SessionId = sessionId,
        };
		return Send<SolverSessionDestroy>(request, token);
    }

    public Task<SolverSessionList?> SessionList(CancellationToken token)
    {
        var request = new SolverRequest
        {
            Command = SolverRequest.CMD_SESSION_LIST
        };
		return Send<SolverSessionList>(request, token);
    }

	private async Task<T?> Send<T>(SolverRequest request, CancellationToken token)
		where T : SolverBaseResponse
	{
		try
		{
			var response = await _api.Post<T, SolverRequest>(ServerUrl, request, token: token);
			if (response?.Status.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
				throw new HttpRequestException($"FlareSolverr {request.Command} failed: {SummarizeError(response.Message)}");

			return response;
		}
		catch (HttpInvalidCodeException ex)
		{
			var detail = ErrorMessage(ex.Body);
			throw new HttpRequestException(
				$"FlareSolverr {request.Command} failed (HTTP {ex.Code}): {detail}",
				ex,
				(HttpStatusCode)ex.Code);
		}
	}

	private static string ErrorMessage(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
			return "The solver returned no JSON error message.";

		try
		{
			using var document = JsonDocument.Parse(body);
			if (document.RootElement.ValueKind == JsonValueKind.Object &&
				document.RootElement.TryGetProperty("message", out var message) &&
				message.ValueKind == JsonValueKind.String)
				return SummarizeError(message.GetString());
		}
		catch (JsonException)
		{
			// Non-JSON gateway errors do not contain a solver diagnostic to extract.
		}

		return "The solver returned no JSON error message.";
	}

	private static string SummarizeError(string? message)
	{
		const int MAX_ERROR_LENGTH = 512;
		if (string.IsNullOrWhiteSpace(message))
			return "The solver returned no error message.";

		// Selenium appends a browser stack trace. Keep its first diagnostic line,
		// rather than logging full response bodies that may contain session data.
		var firstLine = message.Split(["\r", "\n", "\\n"], StringSplitOptions.None)[0];
		return firstLine[..Math.Min(firstLine.Length, MAX_ERROR_LENGTH)];
	}
}
