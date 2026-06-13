using System.Threading.RateLimiting;
using System.Net.Sockets;

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

			var handler = uri.Scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase)
				? Socks5Handler(uri, config.Username, config.Password)
				: ProxyHandler(uri, config.Username, config.Password);

			return new(config.Name, url, handler, limiter);
		}

		public HttpClient CreateClient() => new(Handler, false);

		private static SocketsHttpHandler ProxyHandler(Uri uri, string? username, string? password)
		{
			var proxy = new WebProxy(uri);
			if (!string.IsNullOrWhiteSpace(username))
				proxy.Credentials = new NetworkCredential(username, password);

			return new()
			{
				Proxy = proxy,
				UseProxy = true,
				AutomaticDecompression = DecompressionMethods.All
			};
		}

		private static SocketsHttpHandler Socks5Handler(Uri uri, string? username, string? password)
		{
			(string Username, string Password)? credentials = string.IsNullOrWhiteSpace(username)
				? null
				: (username!, password ?? string.Empty);

			return new()
			{
				UseProxy = false,
				AutomaticDecompression = DecompressionMethods.All,
				ConnectCallback = async (context, token) =>
				{
					var client = new TcpClient();
					await client.ConnectAsync(uri.Host, uri.Port, token);
					var stream = client.GetStream();

					try
					{
						await EstablishSocks5Tunnel(stream, context.DnsEndPoint, credentials, token);
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

		private static async Task EstablishSocks5Tunnel(
			Stream stream,
			DnsEndPoint destination,
			(string Username, string Password)? credentials,
			CancellationToken token)
		{
			var methods = credentials is null
				? new byte[] { 0x05, 0x01, 0x00 }
				: [0x05, 0x02, 0x00, 0x02];

			await stream.WriteAsync(methods, token);
			var response = await ReadExactly(stream, 2, token);
			if (response[0] != 0x05)
				throw new HttpRequestException("Invalid SOCKS5 greeting response.");

			switch (response[1])
			{
				case 0x00:
					break;
				case 0x02:
					if (credentials is null)
						throw new HttpRequestException("SOCKS5 proxy requires username/password authentication.");
					await AuthenticateSocks5(stream, credentials.Value, token);
					break;
				case 0xFF:
					throw new HttpRequestException("SOCKS5 proxy did not accept any supported authentication method.");
				default:
					throw new HttpRequestException($"SOCKS5 proxy selected unsupported authentication method: {response[1]}.");
			}

			var host = Encoding.ASCII.GetBytes(destination.Host);
			if (host.Length > byte.MaxValue)
				throw new HttpRequestException("SOCKS5 destination host is too long.");

			var request = new byte[7 + host.Length];
			request[0] = 0x05;
			request[1] = 0x01;
			request[2] = 0x00;
			request[3] = 0x03;
			request[4] = (byte)host.Length;
			host.CopyTo(request.AsSpan(5));
			request[^2] = (byte)(destination.Port >> 8);
			request[^1] = (byte)(destination.Port & 0xFF);
			await stream.WriteAsync(request, token);

			response = await ReadExactly(stream, 4, token);
			if (response[0] != 0x05)
				throw new HttpRequestException("Invalid SOCKS5 connect response.");
			if (response[1] != 0x00)
				throw new HttpRequestException($"SOCKS5 proxy failed to connect to destination. Reply code: {response[1]}.");

			var addressLength = response[3] switch
			{
				0x01 => 4,
				0x03 => (await ReadExactly(stream, 1, token))[0],
				0x04 => 16,
				_ => throw new HttpRequestException($"SOCKS5 proxy returned unsupported address type: {response[3]}.")
			};

			await ReadExactly(stream, addressLength + 2, token);
		}

		private static async Task AuthenticateSocks5(
			Stream stream,
			(string Username, string Password) credentials,
			CancellationToken token)
		{
			var username = Encoding.UTF8.GetBytes(credentials.Username);
			var password = Encoding.UTF8.GetBytes(credentials.Password);
			if (username.Length > byte.MaxValue || password.Length > byte.MaxValue)
				throw new HttpRequestException("SOCKS5 username and password must each be 255 bytes or fewer.");

			var request = new byte[3 + username.Length + password.Length];
			request[0] = 0x01;
			request[1] = (byte)username.Length;
			username.CopyTo(request.AsSpan(2));
			var passwordLengthIndex = 2 + username.Length;
			request[passwordLengthIndex] = (byte)password.Length;
			password.CopyTo(request.AsSpan(passwordLengthIndex + 1));
			await stream.WriteAsync(request, token);

			var response = await ReadExactly(stream, 2, token);
			if (response[0] != 0x01 || response[1] != 0x00)
				throw new HttpRequestException("SOCKS5 proxy rejected the configured username/password.");
		}

		private static async Task<byte[]> ReadExactly(Stream stream, int count, CancellationToken token)
		{
			var buffer = new byte[count];
			await stream.ReadExactlyAsync(buffer, token);
			return buffer;
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
