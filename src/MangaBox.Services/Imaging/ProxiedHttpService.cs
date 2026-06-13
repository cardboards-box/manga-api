using System.Threading.RateLimiting;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;

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

		DownloadResult? last = null;
		for (var i = 0; i < endpoints.Length; i++)
		{
			var (endpoint, lease) = await Acquire(endpoints, token);
			using var _ = lease;

			_logger.LogDebug("Downloading {Url} through proxy {ProxyName}: {ProxyUrl}", url, endpoint.Name, endpoint.Url);
			var result = await _http.Download(url, headers, request =>
			{
				request.ClientFactory(_ => endpoint.CreateClient());
			}, token);

			if (string.IsNullOrWhiteSpace(result.Error) && result.Stream is not null)
				return result;

			last?.Dispose();
			last = result;

			if (!IsProxyTunnelError(result.Error))
				return result;

			_logger.LogWarning(
				"Proxy {ProxyName} failed to download {Url} through {ProxyUrl}: {Error}",
				endpoint.Name,
				url,
				endpoint.Url,
				result.Error);

			if (IsProxyAuthenticationError(result.Error))
				return result;
		}

		return last ?? new([], url, headers, "All proxy downloads failed");
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

	private static bool IsProxyTunnelError(string? error)
	{
		return !string.IsNullOrWhiteSpace(error) &&
			(error.Contains("proxy", StringComparison.OrdinalIgnoreCase) ||
			 error.Contains("tunnel", StringComparison.OrdinalIgnoreCase) ||
			 error.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase) ||
			 error.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsProxyAuthenticationError(string? error)
	{
		return !string.IsNullOrWhiteSpace(error) &&
			(error.Contains("407", StringComparison.OrdinalIgnoreCase) ||
			 error.Contains("Proxy Authentication", StringComparison.OrdinalIgnoreCase));
	}

	private async Task<ProxyEndpoint[]> NordVpnEndpoints(CancellationToken token)
	{
		var config = NordVpnConfig();
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
			config.Normalize();
			var hosts = NordVpnProxyHosts(doc.RootElement, config);
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

			_logger.LogInformation(
				"Loaded {Count} NordVPN HTTP proxy endpoints using {HostSource} hosts on {Scheme}:{Port}. Credential diagnostics: userLength={UserLength}, passwordLength={PasswordLength}, fingerprint={Fingerprint}",
				endpoints.Length,
				config.UseProxySslHosts ? "proxy_ssl" : "server",
				config.Scheme,
				config.Port,
				config.Username?.Length ?? 0,
				config.Password?.Length ?? 0,
				CredentialFingerprint(config.Username, config.Password));
			return endpoints;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load NordVPN HTTP proxy endpoints from {Url}", config.ServersUrl);
			return [];
		}
	}

	private NordVpnProxyConfig? NordVpnConfig()
	{
		var config = _config.GetSection("Proxies:NordVPN").Get<NordVpnProxyConfig>();
		var source = "Proxies:NordVPN";

		if (config is null)
		{
			config = new();
			source = "defaults";
		}

		var username = FirstConfigValue(
			"Proxies:NordVPN:Username",
			"Proxies:Username",
			"NordVPN:Username",
			"NordVPN__Username",
			"NORDVPN_USERNAME",
			"NORDVPN_USER",
			"NORD_USER");
		var password = FirstConfigValue(
			"Proxies:NordVPN:Password",
			"Proxies:Password",
			"NordVPN:Password",
			"NordVPN__Password",
			"NORDVPN_PASSWORD",
			"NORDVPN_PASS",
			"NORD_PASS");

		if (username is not null || password is not null)
		{
			config.Username = username ?? config.Username;
			config.Password = password ?? config.Password;
			source = "named keys";
		}

		var legacy = LegacyNordVpnConfig();
		if ((string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password)) && legacy is not null)
		{
			config.Username ??= legacy.Username;
			config.Password ??= legacy.Password;
			config.RateLimits = legacy.RateLimits;
			config.PeriodSeconds = legacy.PeriodSeconds;
			source = "legacy Proxies array";
		}

		_logger.LogInformation("NordVPN proxy configuration loaded from {Source}.", source);
		return config;
	}

	private string? FirstConfigValue(params string[] keys)
	{
		foreach (var key in keys)
		{
			var value = _config[key];
			if (value is not null)
				return value;
		}

		return null;
	}

	private ProxyConfig? LegacyNordVpnConfig()
	{
		var children = _config.GetSection("Proxies").GetChildren().ToArray();
		if (children.Length == 0 || children.Any(x => !int.TryParse(x.Key, out _)))
			return null;

		return _config
			.GetSection("Proxies")
			.Get<ProxyConfig[]>()
			?.FirstOrDefault(x => x.Name.Contains("Nord", StringComparison.OrdinalIgnoreCase));
	}

	private static string[] NordVpnProxyHosts(JsonElement root, NordVpnProxyConfig config)
	{
		if (root.ValueKind != JsonValueKind.Array)
			return [];

		var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var server in root.EnumerateArray())
		{
			if (!JsonString(server, "status").Equals("online", StringComparison.OrdinalIgnoreCase))
				continue;

			var host = config.UseProxySslHosts
				? ProxySslHost(server)
				: RegularProxyHost(server);
			if (!string.IsNullOrWhiteSpace(host))
				hosts.Add(host);
		}

		return [..hosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
	}

	private static string? ProxySslHost(JsonElement server)
	{
		if (!server.TryGetProperty("technologies", out var technologies) ||
			technologies.ValueKind != JsonValueKind.Array)
			return null;

		foreach (var technology in technologies.EnumerateArray())
		{
			if (!JsonString(technology, "identifier").Equals("proxy_ssl", StringComparison.OrdinalIgnoreCase) ||
				!PivotIsOnline(technology))
				continue;

			var host = ProxyHost(technology);
			if (!string.IsNullOrWhiteSpace(host))
				return host;
		}

		return null;
	}

	private static string? RegularProxyHost(JsonElement server)
	{
		if (!HasService(server, "proxy"))
			return null;

		return JsonString(server, "hostname");
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

	private static bool HasService(JsonElement server, string service)
	{
		if (!server.TryGetProperty("services", out var services) ||
			services.ValueKind != JsonValueKind.Array)
			return false;

		foreach (var item in services.EnumerateArray())
		{
			if (JsonString(item, "identifier").Equals(service, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static string JsonString(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) &&
			value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;
	}

	private static string CredentialFingerprint(string? username, string? password)
	{
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
			return "missing";

		var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{username}:{password}"));
		return Convert.ToHexString(hash)[..12];
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
		public bool UseProxySslHosts { get; set; } = true;
		public string Scheme { get; set; } = "https";
		public int Port { get; set; } = 89;
		public string? Username { get; set; }
		public string? Password { get; set; }
		public int RateLimits { get; set; } = 10;
		public double PeriodSeconds { get; set; } = 20;
		public int TimeoutSeconds { get; set; } = 30;
		public int MaxServers { get; set; } = 100;

		public void Normalize()
		{
			if (Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && Port == 89)
				UseProxySslHosts = true;
		}
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

			var credentials = Credentials(uri, config.Username, config.Password);
			uri = WithoutUserInfo(uri);
			var handler = ProxyHandler(uri, credentials);

			return new(config.Name, uri.ToString(), handler, limiter);
		}

		public HttpClient CreateClient() => new(Handler, false);

		private static SocketsHttpHandler ProxyHandler(Uri uri, NetworkCredential? credentials)
		{
			return HttpProxyTunnelHandler(uri, BasicProxyAuthorization(credentials));
		}

		private static SocketsHttpHandler HttpProxyTunnelHandler(Uri uri, string? proxyAuthorization)
		{
			return new()
			{
				UseProxy = false,
				AutomaticDecompression = DecompressionMethods.All,
				ConnectCallback = async (context, token) =>
				{
					var client = new TcpClient();
					await client.ConnectAsync(uri.Host, uri.Port, token);
					Stream stream = client.GetStream();

					try
					{
						if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
						{
							var ssl = new SslStream(stream);
							await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
							{
								TargetHost = uri.Host
							}, token);
							stream = ssl;
						}

						await EstablishHttpProxyTunnel(uri, stream, context.DnsEndPoint, proxyAuthorization, token);
						return new DisposingStream(stream, client);
					}
					catch
					{
						await stream.DisposeAsync();
						client.Dispose();
						throw;
					}
				}
			};
		}

		private static async Task EstablishHttpProxyTunnel(
			Uri proxy,
			Stream stream,
			DnsEndPoint destination,
			string? proxyAuthorization,
			CancellationToken token)
		{
			var authority = Authority(destination);
			var builder = new StringBuilder()
				.Append("CONNECT ").Append(authority).Append(" HTTP/1.1\r\n")
				.Append("Host: ").Append(authority).Append("\r\n")
				.Append("Proxy-Connection: Keep-Alive\r\n");

			if (!string.IsNullOrWhiteSpace(proxyAuthorization))
				builder.Append("Proxy-Authorization: ").Append(proxyAuthorization).Append("\r\n");

			builder.Append("\r\n");

			var request = Encoding.ASCII.GetBytes(builder.ToString());
			await stream.WriteAsync(request, token);
			await stream.FlushAsync(token);

			var header = await ReadHttpHeader(proxy, stream, token);
			var status = ParseStatusCode(header);
			if (status is < 200 or > 299)
				throw new HttpRequestException($"The proxy tunnel request to proxy '{proxy}' failed with status code '{status}': {ProxyErrorDetails(header)}");
		}

		private static string Authority(DnsEndPoint endpoint)
		{
			var host = endpoint.Host.Contains(':', StringComparison.Ordinal)
				? $"[{endpoint.Host}]"
				: endpoint.Host;

			return $"{host}:{endpoint.Port}";
		}

		private static async Task<string> ReadHttpHeader(Uri proxy, Stream stream, CancellationToken token)
		{
			var buffer = new List<byte>();
			var single = new byte[1];
			while (buffer.Count < 64 * 1024)
			{
				var read = await stream.ReadAsync(single, token);
				if (read == 0)
					throw new HttpRequestException($"The proxy '{proxy}' closed the connection before returning tunnel headers.");

				buffer.Add(single[0]);
				if (buffer.Count >= 4 &&
					buffer[^4] == '\r' &&
					buffer[^3] == '\n' &&
					buffer[^2] == '\r' &&
					buffer[^1] == '\n')
					return Encoding.ASCII.GetString([..buffer]);
			}

			throw new HttpRequestException($"The proxy '{proxy}' returned tunnel headers larger than 64KB.");
		}

		private static int ParseStatusCode(string header)
		{
			var line = header.Split(["\r\n"], StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
			var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
				? code
				: 0;
		}

		private static string ProxyErrorDetails(string header)
		{
			var lines = header
				.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries)
				.Where(x =>
					x.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) ||
					x.StartsWith("Proxy-Authenticate:", StringComparison.OrdinalIgnoreCase) ||
					x.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))
				.ToArray();

			return lines.Length == 0
				? "No proxy response details."
				: string.Join(" | ", lines);
		}

		private static NetworkCredential? Credentials(Uri uri, string? username, string? password)
		{
			if (!string.IsNullOrEmpty(username))
				return new(username, password);

			if (string.IsNullOrWhiteSpace(uri.UserInfo))
				return null;

			var parts = uri.UserInfo.Split(':', 2);
			var user = Uri.UnescapeDataString(parts[0]);
			var pass = parts.Length > 1
				? Uri.UnescapeDataString(parts[1])
				: string.Empty;

			return string.IsNullOrEmpty(user) ? null : new(user, pass);
		}

		private static string? BasicProxyAuthorization(NetworkCredential? credentials)
		{
			if (credentials is null)
				return null;

			var raw = $"{credentials.UserName}:{credentials.Password}";
			return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))}";
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

	private sealed class DisposingStream(Stream inner, IDisposable owner) : Stream
	{
		public override bool CanRead => inner.CanRead;
		public override bool CanSeek => inner.CanSeek;
		public override bool CanWrite => inner.CanWrite;
		public override long Length => inner.Length;
		public override long Position
		{
			get => inner.Position;
			set => inner.Position = value;
		}

		public override void Flush() => inner.Flush();
		public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
		public override void SetLength(long value) => inner.SetLength(value);
		public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
			inner.ReadAsync(buffer, cancellationToken);
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
			inner.WriteAsync(buffer, cancellationToken);

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				inner.Dispose();
				owner.Dispose();
			}

			base.Dispose(disposing);
		}

		public override async ValueTask DisposeAsync()
		{
			await inner.DisposeAsync();
			owner.Dispose();
			await base.DisposeAsync();
		}
	}
}
