using CardboardBox.Extensions;

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
	private ProxyEndpoint[]? _endpoints;
	private readonly SemaphoreSlim _endpointLock = new(1, 1);
	private int _index = -1;

	public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
	{
		var endpoints = await Endpoints(token);
		if (endpoints.Length == 0)
		{
			_logger.LogWarning("No proxies configured, falling back to direct download");
			return await _http.Download(url, headers, token);
		}

		var (endpoint, lease) = await Acquire(endpoints, token);
		using var _ = lease;

		_logger.LogDebug("Downloading {Url} through proxy {ProxyUrl}", url, endpoint.Url);
		return await _http.Download(url, headers, request =>
		{
			request.ClientFactory(_ => endpoint.CreateClient());
		}, token);
	}

	private async Task<ProxyEndpoint[]> Endpoints(CancellationToken token)
	{
		if (_endpoints is not null)
			return _endpoints;

		await _endpointLock.WaitAsync(token);
		try
		{
			var tokens = _config.GetValue("Proxies:Tokens", 10);
			var seconds = _config.GetValue<double>("Proxies:Seconds", 60);
			var urls = _config.GetSection("Proxies:Urls").Get<string[]>() ?? [];

			return _endpoints ??= [..urls.Select(t => ProxyEndpoint.Create(t, tokens, seconds))
				.Where(t => t is not null)
				.Select(t => t!)];
		}
		finally
		{
			_endpointLock.Release();
		}
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
}