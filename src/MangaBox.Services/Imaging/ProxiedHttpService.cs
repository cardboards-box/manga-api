using System.Threading.RateLimiting;

namespace MangaBox.Services.Imaging;

using Headers = Dictionary<string, string>;
using Config = (string[] Urls, int Tokens, double Seconds);

/// <summary>
/// A service for fetching images through a proxy
/// </summary>
public interface IProxiedHttpService : IDownloadService
{
	/// <summary>
	/// Gets the configuration for the proxy endpoints
	/// </summary>
	/// <returns>The proxy configuration</returns>
	Config GetConfig();

    /// <summary>
    /// Acquires a proxy endpoint and its associated rate limit lease
    /// </summary>
    /// <param name="token">The cancellation token</param>
    /// <returns>A tuple containing the proxy endpoint and its rate limit lease</returns>
    Task<(ProxyEndpoint endpoint, RateLimitLease lease)> Aquire(CancellationToken token);
}

internal class ProxiedHttpService(
	IHttpService _http,
	IConfiguration _config,
	ILogger<ProxiedHttpService> _logger) : IProxiedHttpService
{
	private ProxyEndpoint[]? _endpoints;
	private readonly SemaphoreSlim _endpointLock = new(1, 1);
	private int _index = -1;

	public Config GetConfig()
	{
		var urls = _config.GetSection("Proxies:Urls").Get<string[]>() ?? [];
		var tokens = _config.GetValue("Proxies:Tokens", 120);
		var seconds = _config.GetValue<double>("Proxies:Seconds", 10);
		return (urls, tokens, seconds);
	}

	public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
	{
		var (endpoint, lease) = await Aquire(token);
		using var _ = lease;

		_logger.LogDebug("Downloading {Url} through proxy {ProxyUrl}", url, endpoint.Url);
		return await _http.Download(url, headers, request =>
		{
			request.ClientFactory(_ => endpoint.CreateClient());
		}, token);
	}

	public async Task<(ProxyEndpoint endpoint, RateLimitLease lease)> Aquire(CancellationToken token)
	{
		var endpoints = await Endpoints(token);
		if (endpoints.Length == 0)
		{
			_logger.LogWarning("No proxies configured, cannot acquire endpoint");
            throw new InvalidOperationException("No proxies configured");
        }

		return await Aquire(endpoints, token);
    }

	private async Task<ProxyEndpoint[]> Endpoints(CancellationToken token)
	{
		if (_endpoints is not null)
			return _endpoints;

		await _endpointLock.WaitAsync(token);
		try
		{
			var (urls, tokens, seconds) = GetConfig();
			return _endpoints ??= [..urls.Select(t => ProxyEndpoint.Create(t, tokens, seconds))
				.Where(t => t is not null)
				.Select(t => t!)];
		}
		finally
		{
			_endpointLock.Release();
		}
	}

	private async Task<(ProxyEndpoint endpoint, RateLimitLease lease)> Aquire(ProxyEndpoint[] endpoints, CancellationToken token)
	{
		var start = NextIndex(endpoints.Length);

		for (var i = 0; i < endpoints.Length; i++)
		{
			var endpoint = endpoints[(start + i) % endpoints.Length];
			var lease = endpoint.Limiter.AttemptAcquire(1);
			if (lease.IsAcquired)
				return (endpoint, lease);

			lease.Dispose();
		}

		var fallback = endpoints[start];
		var acquired = await fallback.Limiter.AcquireAsync(1, token);
		return (fallback, acquired);
	}

	private int NextIndex(int length)
	{
		var next = Interlocked.Increment(ref _index);
		if (next < 0)
			next = Interlocked.Exchange(ref _index, 0);

		return next % length;
	}
}

/// <summary>
/// Represents a single configured proxy endpoint
/// </summary>
/// <param name="Url">The proxy URL</param>
/// <param name="Handler">The HTTP handler to use</param>
/// <param name="Limiter">The rate limiter for the proxy</param>
public sealed record ProxyEndpoint(
    string Url,
    SocketsHttpHandler Handler,
    RateLimiter Limiter)
{
    /// <summary>
    /// Creates a new proxy endpoint from the given URL, token limit, and replenishment period
    /// </summary>
    /// <param name="url">The URL of the proxy</param>
    /// <param name="tokens">The maximum number of tokens for the rate limiter</param>
    /// <param name="seconds">The replenishment period in seconds for the rate limiter</param>
    /// <returns>A new instance of <see cref="ProxyEndpoint"/> or null if the URL is invalid</returns>
    public static ProxyEndpoint? Create(string url, int tokens, double seconds)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var limiter = new TokenBucketRateLimiter(new()
        {
            TokenLimit = tokens,
            TokensPerPeriod = tokens,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            ReplenishmentPeriod = TimeSpan.FromSeconds(seconds),
            AutoReplenishment = true
        });

        var handler = ProxyHandler(
            WithoutUserInfo(uri),
            Credentials(uri));

        return new(Redact(uri), handler, limiter);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> instance using the proxy handler.
    /// </summary>
    /// <returns>A new instance of <see cref="HttpClient"/> configured with the proxy handler</returns>
    public HttpClient CreateClient() => new(Handler, false);

    private static SocketsHttpHandler ProxyHandler(Uri uri, NetworkCredential? credentials)
    {
        var proxy = new WebProxy(uri);
        if (credentials is not null)
            proxy.Credentials = credentials;

        return new()
        {
            Proxy = proxy,
            UseProxy = true,
            AutomaticDecompression = DecompressionMethods.All,
        };
    }

    private static NetworkCredential? Credentials(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.UserInfo))
            return null;

        var parts = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(parts[0]);
        var pass = parts.Length > 1
            ? Uri.UnescapeDataString(parts[1])
            : string.Empty;

        return string.IsNullOrWhiteSpace(user) ? null : new(user, pass);
    }

    private static Uri WithoutUserInfo(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.UserInfo))
            return uri;

        return new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty
        }.Uri;
    }

    private static string Redact(Uri uri)
    {
        return string.IsNullOrWhiteSpace(uri.UserInfo)
            ? uri.ToString()
            : WithoutUserInfo(uri).ToString();
    }
}