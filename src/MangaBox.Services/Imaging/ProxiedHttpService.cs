using System.Threading.RateLimiting;

namespace MangaBox.Services.Imaging;

using Utilities.Flare.Models;

using Headers = Dictionary<string, string>;
using Config = (string[] Urls, int Tokens, double Seconds);

/// <summary>
/// A service for fetching images through a proxy
/// </summary>
public interface IProxiedHttpService : IDownloadService
{
	/// <summary>
	/// Internal header used to carry a stable proxy-affinity key to a source.
	/// </summary>
	const string AFFINITY_HEADER = "MangaBox-Proxy-Affinity";

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

	/// <summary>
	/// Downloads a resource through a proxy reserved for an affinity key.
	/// </summary>
	/// <param name="workload">The configuration and reservation workload</param>
	/// <param name="affinity">The stable key whose requests must use the same proxy</param>
	/// <param name="url">The URL to download</param>
	/// <param name="headers">The headers to send</param>
	/// <param name="token">The cancellation token</param>
	Task<DownloadResult> DownloadAffinitized(
		string workload,
		string affinity,
		string url,
		Headers? headers,
		CancellationToken token);
}

internal class ProxiedHttpService(
	IHttpService _http,
	IConfiguration _config,
	ILogger<ProxiedHttpService> _logger) : IProxiedHttpService
{
	private const double DEFAULT_FAILURE_COOLDOWN_SECONDS = 120;

	private ProxyEndpoint[]? _endpoints;
	private readonly SemaphoreSlim _endpointLock = new(1, 1);
	private readonly Lock _affinityLock = new();
	private readonly Dictionary<string, AffinityReservation> _affinities = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, AffinityReservation> _reservedEndpoints = new(StringComparer.OrdinalIgnoreCase);
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
		return await Download(endpoint, lease, url, headers, null, false, null, token);
	}

	public async Task<DownloadResult> DownloadAffinitized(
		string workload,
		string affinity,
		string url,
		Headers? headers,
		CancellationToken token)
	{
		var endpoints = await Endpoints(token);
		if (endpoints.Length == 0)
		{
			_logger.LogWarning("No proxies configured, cannot acquire endpoint");
			throw new InvalidOperationException("No proxies configured");
		}

		var configKey = $"Proxies:Affinity:{workload}";
		var requestSpacing = TimeSpan.FromSeconds(Math.Max(
			0.1,
			_config.GetValue($"{configKey}:RequestSpacingSeconds", 15d)));
		var idleTimeout = TimeSpan.FromSeconds(Math.Max(
			0,
			_config.GetValue($"{configKey}:IdleSeconds", 30d)));
		var batchSize = Math.Max(1, _config.GetValue($"{configKey}:BatchSize", 5));
		var batchWindow = TimeSpan.FromSeconds(Math.Max(
			requestSpacing.TotalSeconds,
			_config.GetValue($"{configKey}:BatchWindowSeconds", 120d)));

		while (true)
		{
			var affinityLease = await AcquireAffinity(
				endpoints,
				workload,
				affinity,
				idleTimeout,
				token);
			var endpoint = affinityLease.Endpoint;
			if (endpoint.IsCoolingDown(workload))
			{
				affinityLease.Dispose();
				continue;
			}

			RateLimitLease requestLease;
			try
			{
				requestLease = await endpoint.AffinityLimiter(
					workload,
					requestSpacing,
					batchSize,
					batchWindow).AcquireAsync(1, token);
			}
			catch
			{
				affinityLease.Dispose();
				throw;
			}

			if (!requestLease.IsAcquired || endpoint.IsCoolingDown(workload))
			{
				requestLease.Dispose();
				affinityLease.Dispose();
				continue;
			}

			return await Download(
				endpoint,
				requestLease,
				url,
				headers,
				workload,
				true,
				affinityLease,
				token);
		}
	}

	private async Task<DownloadResult> Download(
		ProxyEndpoint endpoint,
		RateLimitLease lease,
		string url,
		Headers? headers,
		string? workload,
		bool releaseLeaseWithResult,
		IDisposable? additionalLease,
		CancellationToken token)
	{
		try
		{
			_logger.LogDebug("Downloading {Url} through proxy {ProxyUrl}", url, endpoint.Url);
			var result = await _http.Download(url, WithoutInternalHeaders(headers), request =>
			{
				request.ClientFactory(_ => endpoint.CreateClient());
			}, token);

			if (workload is not null &&
				result.Response?.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
			{
				var cooldown = FailureCooldown(result.Response);
				endpoint.Cooldown(workload, cooldown);
				_logger.LogWarning(
					"Proxy {ProxyUrl} received {StatusCode} from {Host}; pausing it for {CooldownSeconds:F1} seconds",
					endpoint.Url,
					(int)result.Response.StatusCode,
					Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url,
					cooldown.TotalSeconds);
			}

			if (!releaseLeaseWithResult)
			{
				lease.Dispose();
				return result;
			}

			return result with
			{
				Disposables = additionalLease is null
					? result.Disposables.Append(lease)
					: result.Disposables.Append(lease).Append(additionalLease)
			};
		}
		catch
		{
			lease.Dispose();
			additionalLease?.Dispose();
			throw;
		}
	}

	private TimeSpan FailureCooldown(HttpResponseMessage response)
	{
		var configured = TimeSpan.FromSeconds(Math.Max(
			0.1,
			_config.GetValue("Proxies:FailureCooldownSeconds", DEFAULT_FAILURE_COOLDOWN_SECONDS)));
		var retryAfter = response.Headers.RetryAfter;
		var requested = retryAfter?.Delta ??
			(retryAfter?.Date - DateTimeOffset.UtcNow);

		return requested.HasValue && requested.Value > configured
			? requested.Value
			: configured;
	}

	private async Task<AffinityLease> AcquireAffinity(
		ProxyEndpoint[] endpoints,
		string workload,
		string affinity,
		TimeSpan idleTimeout,
		CancellationToken token)
	{
		var affinityKey = $"{workload}\n{affinity}";
		while (true)
		{
			token.ThrowIfCancellationRequested();
			TimeSpan delay;
			lock (_affinityLock)
			{
				var now = DateTime.UtcNow;
				RemoveExpiredAffinityReservations(now);

				AffinityReservation? coolingReservation = null;
				if (_affinities.TryGetValue(affinityKey, out var existing))
				{
					if (!existing.Endpoint.IsCoolingDown(workload))
					{
						existing.ActiveRequests++;
						return new(this, existing);
					}

					coolingReservation = existing;
				}

				if (coolingReservation is null)
				{
					var start = NextIndex(endpoints.Length);
					for (var i = 0; i < endpoints.Length; i++)
					{
						var endpoint = endpoints[(start + i) % endpoints.Length];
						var endpointKey = $"{workload}\n{endpoint.Url}";
						if (endpoint.IsCoolingDown(workload) || _reservedEndpoints.ContainsKey(endpointKey))
							continue;

						var reservation = new AffinityReservation(
							affinityKey,
							endpointKey,
							workload,
							endpoint,
							idleTimeout)
						{
							ActiveRequests = 1
						};
						_affinities[affinityKey] = reservation;
						_reservedEndpoints[endpointKey] = reservation;
						_logger.LogDebug(
							"Reserved proxy {ProxyUrl} for {Workload} affinity {Affinity}",
							endpoint.Url,
							workload,
							affinity);
						return new(this, reservation);
					}
				}

				delay = coolingReservation?.Endpoint.CooldownRemaining(workload) ??
					_reservedEndpoints.Values
						.Where(x => x.Workload.Equals(workload, StringComparison.OrdinalIgnoreCase) && x.ActiveRequests == 0)
						.Select(x => x.IdleUntilUtc - now)
						.Concat(endpoints.Select(x => x.CooldownRemaining(workload)))
						.Where(x => x > TimeSpan.Zero)
						.DefaultIfEmpty(TimeSpan.FromMilliseconds(250))
						.Min();
			}

			await Task.Delay(delay, token);
		}
	}

	private void ReleaseAffinity(AffinityReservation reservation)
	{
		lock (_affinityLock)
		{
			if (reservation.ActiveRequests > 0)
				reservation.ActiveRequests--;
			if (reservation.ActiveRequests == 0)
			{
				var cooldown = reservation.Endpoint.CooldownRemaining(reservation.Workload);
				var hold = cooldown + reservation.IdleTimeout;
				reservation.IdleUntilUtc = DateTime.UtcNow + hold;
			}
		}
	}

	private void RemoveExpiredAffinityReservations(DateTime now)
	{
		var expired = _affinities.Values
			.Where(x => x.ActiveRequests == 0 && x.IdleUntilUtc <= now)
			.ToArray();
		foreach (var reservation in expired)
			RemoveAffinityReservation(reservation);
	}

	private void RemoveAffinityReservation(AffinityReservation reservation)
	{
		_affinities.Remove(reservation.AffinityKey);
		_reservedEndpoints.Remove(reservation.EndpointKey);
	}

	private static Headers? WithoutInternalHeaders(Headers? headers)
	{
		if (headers is null || !headers.ContainsKey(IProxiedHttpService.AFFINITY_HEADER))
			return headers;

		var output = new Headers(headers, StringComparer.OrdinalIgnoreCase);
		output.Remove(IProxiedHttpService.AFFINITY_HEADER);
		return output;
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

	private async Task<(ProxyEndpoint endpoint, RateLimitLease lease)> Aquire(
		ProxyEndpoint[] endpoints,
		CancellationToken token)
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

	private sealed record AffinityReservation(
		string AffinityKey,
		string EndpointKey,
		string Workload,
		ProxyEndpoint Endpoint,
		TimeSpan IdleTimeout)
	{
		public int ActiveRequests { get; set; }
		public DateTime IdleUntilUtc { get; set; } = DateTime.MaxValue;
	}

	private sealed class AffinityLease : IDisposable
	{
		private readonly ProxiedHttpService _owner;
		private AffinityReservation? _reservation;

		public AffinityLease(ProxiedHttpService owner, AffinityReservation reservation)
		{
			_owner = owner;
			_reservation = reservation;
			Endpoint = reservation.Endpoint;
		}

		public ProxyEndpoint Endpoint { get; }

		public void Dispose()
		{
			var current = Interlocked.Exchange(ref _reservation, null);
			if (current is not null)
				_owner.ReleaseAffinity(current);
		}
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
	private SolverProxy? _solverProxy;
	private readonly ConcurrentDictionary<string, long> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<string, RateLimiter> _batchLimiters = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Whether this endpoint is temporarily unavailable for a workload after a throttled response.
	/// </summary>
	public bool IsCoolingDown(string workload) => CooldownRemaining(workload) > TimeSpan.Zero;

	/// <summary>
	/// The remaining time before this endpoint can be selected for a workload again.
	/// </summary>
	public TimeSpan CooldownRemaining(string workload)
	{
		var until = _cooldowns.GetValueOrDefault(workload);
		var remaining = new DateTime(until, DateTimeKind.Utc) - DateTime.UtcNow;
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
	}

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

        var proxyUri = WithoutUserInfo(uri);
        var credentials = Credentials(uri);
        var handler = ProxyHandler(proxyUri, credentials);
        var solverProxy = new SolverProxy
        {
            // Chromium expects proxy-server values in scheme://host:port form.
            // Uri.ToString() appends a trailing slash, which causes Chromium to
            // return ERR_NO_SUPPORTED_PROXIES for SOCKS proxies.
            Url = proxyUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped),
            Username = credentials?.UserName,
            Password = credentials?.Password,
        };

        return new(Redact(uri), handler, limiter)
        {
            _solverProxy = solverProxy,
        };
    }

	/// <summary>
	/// Prevents this endpoint from being selected for the specified period.
	/// </summary>
	/// <param name="workload">The workload to pause on this endpoint</param>
	/// <param name="duration">The cooldown duration</param>
	public void Cooldown(string workload, TimeSpan duration)
	{
		if (duration <= TimeSpan.Zero)
			return;

		var target = DateTime.UtcNow.Add(duration).Ticks;
		_cooldowns.AddOrUpdate(workload, target, (_, current) => Math.Max(current, target));
	}

	/// <summary>
	/// Gets a limiter that serializes and spaces an affinity workload into request batches.
	/// </summary>
	/// <param name="workload">The workload name</param>
	/// <param name="requestSpacing">The minimum time between requests</param>
	/// <param name="batchSize">The number of requests allowed in one batch</param>
	/// <param name="batchWindow">The rolling window containing one batch</param>
	public RateLimiter AffinityLimiter(
		string workload,
		TimeSpan requestSpacing,
		int batchSize,
		TimeSpan batchWindow)
	{
		var key = $"{workload}:{requestSpacing.Ticks}:{batchSize}:{batchWindow.Ticks}";
		return _batchLimiters.GetOrAdd(key, _ => RateLimiter.CreateChained(
			new ConcurrencyLimiter(new()
			{
				PermitLimit = 1,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = int.MaxValue
			}),
			new SlidingWindowRateLimiter(new()
			{
				PermitLimit = 1,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = int.MaxValue,
				Window = requestSpacing,
				SegmentsPerWindow = 10,
				AutoReplenishment = true
			}),
			new SlidingWindowRateLimiter(new()
			{
				PermitLimit = batchSize,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = int.MaxValue,
				Window = batchWindow,
				SegmentsPerWindow = 30,
				AutoReplenishment = true
			})));
	}

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> instance using the proxy handler.
    /// </summary>
    /// <returns>A new instance of <see cref="HttpClient"/> configured with the proxy handler</returns>
    public HttpClient CreateClient() => new(Handler, false);

    /// <summary>
    /// Creates the proxy payload expected by FlareSolverr.
    /// </summary>
    /// <returns>A proxy payload with credentials separated from the redacted proxy URL.</returns>
    public SolverProxy CreateSolverProxy()
    {
        var proxy = _solverProxy
            ?? throw new InvalidOperationException("This proxy endpoint was not created by ProxyEndpoint.Create.");

        return new()
        {
            Url = proxy.Url,
            Username = proxy.Username,
            Password = proxy.Password,
        };
    }

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
