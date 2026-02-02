using HtmlAgilityPack;

namespace MangaBox.Utilities.Flare;

using Models;
using RateLimits;
using System.Collections.Specialized;

/// <summary>
/// A rate-limited instance of the Flare solver
/// </summary>
/// <param name="_flare">The solver to use for requests</param>
/// <param name="_logger">The logger for operations</param>
public class FlareSolverInstance(
	IFlareSolverBase _flare,
	ILogger _logger)
{
	private SolverCookie[]? _cookies = null;
	private RateLimiterBase? _rateLimiter = null;
	private (int limit, int timeout)? _limiter = null;
	private readonly Dictionary<string, HtmlDocument> _pageCache = new(StringComparer.InvariantCultureIgnoreCase);

	/// <summary>
	/// The minimum number of requests to make before pausing
	/// </summary>
	public int MaxRequestsBeforePauseMin
	{
		get => field;
		set { field = value; _rateLimiter = null; }
	} = 2;

	/// <summary>
	/// The maximum number of requests to make before pausing
	/// </summary>
	public int MaxRequestsBeforePauseMax
	{
		get => field;
		set { field = value; _rateLimiter = null; }
	} = 6;

	/// <summary>
	/// The minimum number of seconds to pause for
	/// </summary>
	public int PauseDurationSecondsMin
	{
		get => field;
		set { field = value; _rateLimiter = null; }
	} = 15;

	/// <summary>
	/// The maximum number of seconds to pause for
	/// </summary>
	public int PauseDurationSecondsMax
	{
		get => field;
		set { field = value; _rateLimiter = null; }
	} = 35;

	/// <summary>
	/// The maximum number of retries for a request
	/// </summary>
	public int MaxRetries { get; set; } = 4;

	/// <summary>
	/// The rate limiter to use
	/// </summary>
	public virtual RateLimiterBase Limiter => _rateLimiter ??= new(
		new(MaxRequestsBeforePauseMin, MaxRequestsBeforePauseMax),
		new(PauseDurationSecondsMin, PauseDurationSecondsMax));

	/// <summary>
	/// The cookies to use for requests
	/// </summary>
	public string Cookie => _cookies is null
		? string.Empty
		: string.Join("; ", _cookies.Select(c => $"{c.Name}={c.Value}"));

	/// <summary>
	/// Clears the current cookie
	/// </summary>
	public void ClearCookies()
	{
		_cookies = null;
	}

	/// <summary>
	/// Sets the cookie value
	/// </summary>
	/// <param name="key">The key of the cookie to set</param>
	/// <param name="value">The value of the cookie to set</param>
	public void SetCookie(string key, string value)
	{
		_cookies ??= [];

		var cookie = _cookies.FirstOrDefault(c => c.Name == key);
		if (cookie is not null)
		{
			cookie.Value = value;
			return;
		}

		_cookies = [.. _cookies.Append(new SolverCookie(key, value))];
	}

	/// <summary>
	/// Clears the page cache
	/// </summary>
	public void ClearCache()
	{
		_pageCache.Clear();
	}

	/// <summary>
	/// Checks to see if the limits have been reached and pauses if necessary
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	public async Task LimitCheck(CancellationToken token = default)
	{
		if (!Limiter.Enabled || token.IsCancellationRequested) return;

		var (limit, timeout) = _limiter ??= Limiter.GetRateLimit();

		if (Limiter.Rate < limit)
		{
			_logger.LogInformation("Below rate limit. Count: {count} - {rate}/{limit} - {timeout}ms",
				Limiter.Count, Limiter.Rate, limit, timeout);
			Limiter.Count++;
			Limiter.Rate++;
			return;
		}

		_logger.LogInformation("Rate limit reached. Pausing for {timeout}ms. Count: {count} - {rate}/{limit}",
			timeout, Limiter.Count, Limiter.Rate, limit);

		await Task.Delay(timeout, token);
		Limiter.Rate = 0;
		ClearCookies();
		_limiter = Limiter.GetRateLimit();
		_logger.LogInformation("Resuming after pause. New Limits {limit} - {timeout}ms", limit, timeout);
	}

	private async Task<HtmlDocument> DoRequest(string url, bool get, NameValueCollection? body = null, int count = 0)
	{
		try
		{
			_logger.LogInformation("Getting data from {url}", url);
			var data = get 
				? await _flare.Get(url, _cookies, timeout: 30_000)
				: await _flare.Post(url, body ?? [], _cookies, timeout: 30_000);
			if (data is null || data.Solution is null) throw new Exception("Failed to get data");

			if (data.Solution.Status < 200 || data.Solution.Status >= 300)
				throw new Exception($"Failed to get data: {data.Solution.Status}");

			_cookies = data.Solution.Cookies;

			var doc = new HtmlDocument();
			doc.LoadHtml(data.Solution.Response);
			_logger.LogInformation("Got data from {url}", url);
			return doc;
		}
		catch (Exception ex)
		{
			if (count > MaxRetries) throw;

			count++;
			ClearCookies();
			var delay = Random.Shared.Next(PauseDurationSecondsMin, PauseDurationSecondsMax);
			_logger.LogError(ex, "Failed to get data for url {count}/{max}, retrying after {delay} seconds: {url}", count, MaxRetries, delay, url);
			await Task.Delay(delay * 1000);
			_logger.LogInformation("Retrying request");
			return await DoRequest(url, get, body, count);
		}
	}

	/// <summary>
	/// Requests HTML from the given URL
	/// </summary>
	/// <param name="url">The URL to fetch</param>
	/// <param name="cache">Whether or not to cache the page</param>
	/// <returns>The HTML document retrieved from the URL</returns>
	/// <remarks>This does not use <see cref="LimitCheck(CancellationToken)"/></remarks>
	public virtual async Task<HtmlDocument> Get(string url, bool cache = false)
	{
		if (_pageCache.TryGetValue(url, out var doc))
			return doc;

		var page = await DoRequest(url, true, null);
		if (cache) _pageCache[url] = page;
		return page;
	}

	/// <summary>
	/// Requests HTML from the given URL
	/// </summary>
	/// <param name="url">The URL to fetch</param>
	/// <param name="body">The body of the request</param>
	/// <param name="cache">Whether or not to cache the page</param>
	/// <returns>The HTML document retrieved from the URL</returns>
	/// <remarks>This does not use <see cref="LimitCheck(CancellationToken)"/></remarks>
	public virtual async Task<HtmlDocument> Post(string url, NameValueCollection? body = null, bool cache = false)
	{
		if (_pageCache.TryGetValue(url, out var doc))
			return doc;

		var page = await DoRequest(url, false, body);
		if (cache) _pageCache[url] = page;
		return page;
	}
}
