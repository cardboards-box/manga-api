using System.Threading.RateLimiting;

namespace MangaBox.Services.Imaging;

using Headers = Dictionary<string, string>;

/// <summary>
/// A service for fetching images through a proxy
/// </summary>
public interface IProxiedHttpService : IDownloadService { }

internal class ProxiedHttpService(
	IHttpService _http,
	IConfiguration _config,
	ILogger<ProxiedHttpService> _logger) : IProxiedHttpService
{
	private ProxyConfig[]? _proxies;
	private ProxyEndpoint[]? _endpoints;
	private int _index = -1;

	public ProxyConfig[] Proxies => _proxies ??= _config.GetSection("Proxies").Get<ProxyConfig[]>() ?? [];

	public ProxyEndpoint[] Endpoints => _endpoints ??= BuildEndpoints(Proxies);

	public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
	{
		var endpoints = Endpoints;
		if (endpoints.Length == 0)
		{
			_logger.LogWarning("No proxies configured, falling back to direct download");
			return await _http.Download(url, headers, token);
		}

		var (endpoint, lease) = await Acquire(endpoints, token);
		using var _ = lease;

		_logger.LogDebug("Downloading {Url} through proxy {ProxyName}: {ProxyUrl}", url, endpoint.Name, endpoint.Url);
		return await _http.Download(url, headers, request =>
		{
			request.ClientFactory(_ => endpoint.CreateClient());
		}, token);
	}

	private async Task<(ProxyEndpoint endpoint, RateLimitLease lease)> Acquire(ProxyEndpoint[] endpoints, CancellationToken token)
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

	private static ProxyEndpoint[] BuildEndpoints(ProxyConfig[] configs)
	{
		return [..configs
			.Where(c => c.Urls is { Length: > 0 })
			.SelectMany(c => c.Urls
				.Where(u => !string.IsNullOrWhiteSpace(u))
				.Select(u => ProxyEndpoint.Create(c, u)))
			.Where(p => p is not null)
			.Cast<ProxyEndpoint>()];
	}

	/// <summary>
	/// Represents the configuration for a proxy
	/// </summary>
	/// <param name="Name">The name of the proxy provider</param>
	/// <param name="Username">The username for the proxy</param>
	/// <param name="Password">The password for the proxy</param>
	/// <param name="Urls">The URLs of the proxy servers</param>
	/// <param name="RateLimits">The rate limits to apply to each URL</param>
	/// <param name="PeriodSeconds">The period in seconds for the rate limits</param>
	public record class ProxyConfig(
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("username")] string? Username,
		[property: JsonPropertyName("password")] string? Password,
		[property: JsonPropertyName("urls")] string[] Urls,
		[property: JsonPropertyName("rateLimits")] int RateLimits,
		[property: JsonPropertyName("periodSeconds")] double PeriodSeconds);

	/// <summary>
	/// Represents a single configured proxy endpoint
	/// </summary>
	/// <param name="Name">The name of the proxy provider</param>
	/// <param name="Url">The proxy URL</param>
	/// <param name="Handler">The HTTP handler to use</param>
	/// <param name="Limiter">The rate limiter for the proxy</param>
	public sealed record ProxyEndpoint(
		string Name,
		string Url,
		SocketsHttpHandler Handler,
		RateLimiter Limiter)
	{
		public static ProxyEndpoint? Create(ProxyConfig config, string url)
		{
			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
				return null;

			var proxy = new WebProxy(uri);
			if (!string.IsNullOrWhiteSpace(config.Username))
				proxy.Credentials = new NetworkCredential(config.Username, config.Password);

			var handler = new SocketsHttpHandler
			{
				Proxy = proxy,
				UseProxy = true,
				AutomaticDecompression = DecompressionMethods.All
			};

			var tokens = Math.Max(1, config.RateLimits);
			var seconds = Math.Max(1, config.PeriodSeconds);
			var limiter = new TokenBucketRateLimiter(new()
			{
				TokenLimit = tokens,
				TokensPerPeriod = tokens,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = int.MaxValue,
				ReplenishmentPeriod = TimeSpan.FromSeconds(seconds),
				AutoReplenishment = true
			});

			return new(config.Name, url, handler, limiter);
		}

		public HttpClient CreateClient() => new(Handler, false);
	}
}
