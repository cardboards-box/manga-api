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

		_logger.LogDebug("Downloading {Url} through proxy {ProxyName}: {ProxyUrl}", url, endpoint.Name, endpoint.Url);
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
			return _endpoints ??= await NordVpnEndpoints(token);
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

	private async Task<ProxyEndpoint[]> NordVpnEndpoints(CancellationToken token)
	{
		var config = _config.GetSection("Proxies:NordVPN").Get<NordVpnProxyConfig>();
		if (config is null || !config.Enabled)
			return [];

		if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.Password))
		{
			_logger.LogWarning("NordVPN proxies are enabled but Proxies:NordVPN:Username or Proxies:NordVPN:Password is missing.");
			return [];
		}

		try
		{
			using var client = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds))
			};
			using var response = await client.GetAsync(config.ServersUrl, token);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(token);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
			var hosts = NordVpnProxyHosts(doc.RootElement);
			var endpoints = hosts
				.Take(Math.Max(1, config.MaxServers))
				.Select(host => ProxyEndpoint.Create(
					new(
						config.Name,
						config.Username,
						config.Password,
						config.RateLimits,
						config.PeriodSeconds),
					$"{config.Scheme}://{host}:{config.Port}"))
				.Where(x => x is not null)
				.Cast<ProxyEndpoint>()
				.ToArray();

			_logger.LogInformation("Loaded {Count} NordVPN HTTP proxy endpoints.", endpoints.Length);
			return endpoints;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load NordVPN HTTP proxy endpoints from {Url}", config.ServersUrl);
			return [];
		}
	}

	private static string[] NordVpnProxyHosts(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Array)
			return [];

		var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var server in root.EnumerateArray())
		{
			if (!JsonString(server, "status").Equals("online", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!server.TryGetProperty("technologies", out var technologies) ||
				technologies.ValueKind != JsonValueKind.Array)
				continue;

			foreach (var technology in technologies.EnumerateArray())
			{
				if (!JsonString(technology, "identifier").Equals("proxy_ssl", StringComparison.OrdinalIgnoreCase) ||
					!PivotIsOnline(technology))
					continue;

				var host = ProxyHost(technology);
				if (!string.IsNullOrWhiteSpace(host))
					hosts.Add(host);
			}
		}

		return [..hosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
	}

	private static bool PivotIsOnline(JsonElement technology)
	{
		if (!technology.TryGetProperty("pivot", out var pivot) ||
			pivot.ValueKind != JsonValueKind.Object)
			return true;

		return JsonString(pivot, "status").Equals("online", StringComparison.OrdinalIgnoreCase);
	}

	private static string? ProxyHost(JsonElement technology)
	{
		if (!technology.TryGetProperty("metadata", out var metadata) ||
			metadata.ValueKind != JsonValueKind.Array)
			return null;

		foreach (var item in metadata.EnumerateArray())
		{
			if (JsonString(item, "name").Equals("proxy_hostname", StringComparison.OrdinalIgnoreCase))
				return JsonString(item, "value");
		}

		return null;
	}

	private static string JsonString(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) &&
			value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;
	}

	/// <summary>
	/// Represents the configuration for a proxy
	/// </summary>
	/// <param name="Name">The name of the proxy provider</param>
	/// <param name="Username">The username for the proxy</param>
	/// <param name="Password">The password for the proxy</param>
	/// <param name="RateLimits">The rate limits to apply to each URL</param>
	/// <param name="PeriodSeconds">The period in seconds for the rate limits</param>
	public record class ProxyConfig(
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("username")] string? Username,
		[property: JsonPropertyName("password")] string? Password,
		[property: JsonPropertyName("rateLimits")] int RateLimits,
		[property: JsonPropertyName("periodSeconds")] double PeriodSeconds);

	public sealed class NordVpnProxyConfig
	{
		public bool Enabled { get; set; } = true;
		public string Name { get; set; } = "NordVPN";
		public string ServersUrl { get; set; } = "https://api.nordvpn.com/v1/servers";
		public string Scheme { get; set; } = "https";
		public int Port { get; set; } = 89;
		public string? Username { get; set; }
		public string? Password { get; set; }
		public int RateLimits { get; set; } = 10;
		public double PeriodSeconds { get; set; } = 20;
		public int TimeoutSeconds { get; set; } = 30;
		public int MaxServers { get; set; } = 100;
	}

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

			var handler = ProxyHandler(
				WithoutUserInfo(uri),
				Credentials(uri, config.Username, config.Password));

			return new(config.Name, Redact(uri), handler, limiter);
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

		private static NetworkCredential? Credentials(Uri uri, string? username, string? password)
		{
			if (!string.IsNullOrWhiteSpace(username))
				return new(username, password);

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