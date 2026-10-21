namespace MangaBox.Providers.Sources;

using Services.Imaging;

/// <summary>Shares rotating-proxy requests, pacing and rate-limit cooldowns across all WeebCentral sources.</summary>
internal interface IWeebCentralRequestService : IDownloadService { }

/// <summary>
/// Serializes WeebCentral requests across transient provider instances. Every
/// attempt uses normal proxy rotation, including metadata and chapter fragments;
/// a rate limit pauses the whole provider before another endpoint is tried.
/// Responses are owned by callers, except failed responses disposed before retry.
/// </summary>
/// <param name="_proxy">The existing rotating proxy pool, backed by the shared HTTP/API services.</param>
/// <param name="_config">WeebCentral request spacing, backoff and attempt settings.</param>
/// <param name="_logger">The retry and cooldown logger.</param>
/// <param name="timeProvider">Optional clock for the CLI harness; defaults to the system clock.</param>
internal sealed class WeebCentralRequestService(
    IProxiedHttpService _proxy,
    IConfiguration _config,
    ILogger<WeebCentralRequestService> _logger,
    TimeProvider? timeProvider = null) : IWeebCentralRequestService
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequest = DateTimeOffset.MinValue;

    /// <summary>Reads a finite positive duration; invalid configuration retains the conservative default.</summary>
    private double Seconds(string key, double fallback)
    {
        return double.TryParse(_config[$"WeebCentral:{key}"], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            double.IsFinite(value) && value > 0 && value <= 86400 ? value : fallback;
    }

    /// <summary>Recognizes temporary rate limits, origin errors and transport failures without retrying ordinary 4xx errors.</summary>
    private static bool Retryable(DownloadResult result) => result.Response is { } response
        ? response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode is >= 500 and <= 599
        : !string.IsNullOrEmpty(result.Error);

    /// <summary>Computes capped exponential backoff; server Retry-After values are a lower bound and are never shortened.</summary>
    private TimeSpan Backoff(HttpResponseMessage? response, int attempt)
    {
        var initial = Seconds("InitialBackoffSeconds", 60);
        var maximum = Math.Max(initial, Seconds("MaxBackoffSeconds", 300));
        var delay = TimeSpan.FromSeconds(Math.Min(maximum, initial * Math.Pow(2, attempt - 1)));
        var retryAfter = response?.Headers.RetryAfter;
        var requested = retryAfter?.Delta ?? (retryAfter?.Date - _clock.GetUtcNow());
        return requested.HasValue && requested.Value > delay ? requested.Value : delay;
    }

    /// <inheritdoc />
    public async Task<DownloadResult> Download(string url, Dictionary<string, string>? headers, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var attempts = Math.Clamp(_config.GetValue("WeebCentral:MaxAttempts", 4), 1, 8);
            for (var attempt = 1; ; attempt++)
            {
                var wait = _nextRequest - _clock.GetUtcNow();
                if (wait > TimeSpan.Zero) await Task.Delay(wait, _clock, token);
                token.ThrowIfCancellationRequested();

                // Use Download, never DownloadAffinitized: every attempt rotates
                // through the configured pool and there is no direct fallback.
                var result = await _proxy.Download(url, headers, token);
                _nextRequest = _clock.GetUtcNow() + TimeSpan.FromSeconds(Seconds("RequestSpacingSeconds", 2));
                var retry = Retryable(result);
                if (retry)
                {
                    var delay = Backoff(result.Response, attempt);
                    var until = _clock.GetUtcNow() + delay;
                    if (until > _nextRequest) _nextRequest = until;
                    delay = _nextRequest - _clock.GetUtcNow();
                    _logger.LogWarning(
                        "WeebCentral {Failure} for {Url}; provider paused for {BackoffSeconds:F1} seconds ({Attempt}/{MaxAttempts})",
                        result.Response is { } response ? $"HTTP {(int)response.StatusCode}" : result.Error,
                        url, delay.TotalSeconds, attempt, attempts);
                }
                // Record a received rate limit before checking cancellation: a
                // caller disconnecting at the response boundary must not erase it.
                if (token.IsCancellationRequested)
                {
                    result.Dispose();
                    token.ThrowIfCancellationRequested();
                }
                // Keep the cooldown after exhaustion too, so the next chapter or
                // image cannot immediately start another burst of rejected calls.
                if (!retry || attempt == attempts) return result;
                result.Dispose();
            }
        }
        finally { _gate.Release(); }
    }
}
