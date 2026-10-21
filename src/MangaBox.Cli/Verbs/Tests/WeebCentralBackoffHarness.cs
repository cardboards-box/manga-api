using System.Diagnostics;
using System.Net.Http.Headers;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>
/// Runs WeebCentral's scheduler through the real rotating proxy service with
/// in-memory HTTP responses and virtual time. No network, database or real
/// minute-long sleeps are needed to verify rate-limit recovery.
/// </summary>
internal static class WeebCentralBackoffHarness
{
    private const string Url = "https://weebcentral.com/chapters/01J76XZ079BG7SB18GTTX6XQMV";
    private const string FirstProxy = "socks5://127.0.0.1:1080/";
    private const string SecondProxy = "socks5://127.0.0.1:1081/";

    /// <summary>Exercises request routing, retry timing, resource ownership, and cancellation.</summary>
    public static async Task Run(ILogger logger, CancellationToken token)
    {
        var rig = Create((_, call) => call <= 2 ? new(HttpStatusCode.TooManyRequests) : new(HttpStatusCode.OK));
        var source = new WeebCentralSource(rig.Requests);
        var chapter = source.ChapterPages(Url, token);
        await Complete(chapter, rig.Clock, token);
        Require(chapter.Result.Length == 1 && rig.ProxyLog.Downloads.SequenceEqual([FirstProxy, SecondProxy, FirstProxy]),
            "Chapter HTML did not rotate proxies on every attempt.");
        Require(rig.Http.Times.Select(time => (time - rig.Http.Times[0]).TotalSeconds).SequenceEqual([0, 60, 180]),
            "Default exponential backoff must wait 60 then 120 seconds.");
        Require(rig.Http.Disposed == 3, "Failed and parsed HTML responses must all be disposed.");
        var image = new WeebCentralSource(rig.Requests).DownloadImage(new RejectDownload(), "https://images.test/page.jpg", null, token);
        await Complete(image, rig.Clock, token);
        using (image.Result)
            Require(image.Result.Response?.IsSuccessStatusCode == true && rig.ProxyLog.Downloads[^1] == SecondProxy &&
                rig.Http.Times[^1] - rig.Http.Times[^2] == TimeSpan.FromSeconds(2),
                "Images must use rotation and share metadata pacing across provider instances.");

        foreach (var dateHeader in new[] { false, true })
        {
            rig = Create((clock, call) => call == 1 ? new(HttpStatusCode.TooManyRequests,
                dateHeader ? new(clock.GetUtcNow().AddSeconds(660)) : new(TimeSpan.FromSeconds(180))) : new(HttpStatusCode.OK));
            var request = rig.Requests.Download(Url, null, token);
            await Complete(request, rig.Clock, token);
            using (request.Result)
                Require(rig.Http.Times[1] - rig.Http.Times[0] == TimeSpan.FromSeconds(dateHeader ? 660 : 180),
                    "Retry-After delta/date must be honored even beyond the local backoff cap.");
        }

        rig = Create((_, call) => call <= 2 ? new(HttpStatusCode.TooManyRequests) : new(HttpStatusCode.OK), attempts: 2);
        var exhausted = rig.Requests.Download(Url, null, token);
        await Complete(exhausted, rig.Clock, token);
        using (exhausted.Result)
            Require(exhausted.Result.Response?.StatusCode == HttpStatusCode.TooManyRequests && rig.Http.Times.Count == 2,
                "Retry exhaustion must return the failed response and stop at the attempt limit.");
        var next = rig.Requests.Download(Url, null, token);
        await Complete(next, rig.Clock, token);
        using (next.Result)
            Require(rig.Http.Times[2] - rig.Http.Times[1] == TimeSpan.FromSeconds(120),
                "Exhaustion must preserve cooldown for the next chapter.");

        rig = Create((_, _) => new(HttpStatusCode.TooManyRequests), maximum: 90);
        var capped = rig.Requests.Download(Url, null, token);
        await Complete(capped, rig.Clock, token);
        using (capped.Result)
            Require(rig.Http.Times.Select(time => (time - rig.Http.Times[0]).TotalSeconds).SequenceEqual([0, 60, 150, 240]),
                "Exponential backoff must honor the configured local maximum.");

        foreach (var status in new HttpStatusCode?[] { HttpStatusCode.ServiceUnavailable, null, HttpStatusCode.NotFound })
        {
            rig = Create((_, call) => new(call == 1 ? status : HttpStatusCode.OK));
            var request = rig.Requests.Download(Url, null, token);
            await Complete(request, rig.Clock, token);
            using (request.Result)
                Require(rig.Http.Times.Count == (status == HttpStatusCode.NotFound ? 1 : 2),
                    "Retry temporary service/transport failures but never ordinary 404s.");
        }

        // A 429 must pause independent providers and both request types together.
        rig = Create((_, call) => new(call == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK));
        var first = new WeebCentralSource(rig.Requests).ChapterPages(Url, token);
        var second = new WeebCentralSource(rig.Requests).ChapterPages(Url, token);
        var third = new WeebCentralSource(rig.Requests).DownloadImage(new RejectDownload(), "https://images.test/page.jpg", null, token);
        await Complete(Task.WhenAll(first, second, third), rig.Clock, token);
        third.Result.Dispose();
        Require(rig.Http.Times.Count == 4 && rig.Http.Times[1] - rig.Http.Times[0] == TimeSpan.FromSeconds(60) &&
            rig.Http.Times.Zip(rig.Http.Times.Skip(1)).All(pair => pair.Second - pair.First >= TimeSpan.FromSeconds(2)),
            "Concurrent source instances bypassed the shared cooldown or minimum spacing.");

        rig = Create((_, call) => new(call == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var interrupted = rig.Requests.Download(Url, null, cancelled.Token);
        Require(rig.Http.Disposed == 1 && !interrupted.IsCompleted, "The failed response must be disposed before backoff.");
        // A queued caller can also cancel without issuing another HTTP request.
        var queued = rig.Requests.Download(Url, null, cancelled.Token);
        cancelled.Cancel();
        foreach (var task in new[] { interrupted, queued })
        {
            try { using var unexpected = await task; throw new InvalidOperationException("Cancellation was ignored."); }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
        }
        Require(rig.Http.Times.Count == 1, "Cancellation sent an extra request.");
        next = rig.Requests.Download(Url, null, token);
        await Complete(next, rig.Clock, token);
        using (next.Result)
            Require(rig.Http.Times[1] - rig.Http.Times[0] == TimeSpan.FromSeconds(60),
                "Cancellation must release the gate while retaining the rate-limit cooldown.");

        using var boundary = CancellationTokenSource.CreateLinkedTokenSource(token);
        rig = Create((_, call) =>
        {
            if (call != 1) return new(HttpStatusCode.OK);
            boundary.Cancel();
            return new(HttpStatusCode.TooManyRequests);
        });
        try
        {
            using var unexpected = await rig.Requests.Download(Url, null, boundary.Token);
            throw new InvalidOperationException("Response-boundary cancellation was ignored.");
        }
        catch (OperationCanceledException) when (boundary.IsCancellationRequested) { }
        next = rig.Requests.Download(Url, null, token);
        await Complete(next, rig.Clock, token);
        using (next.Result)
            Require(rig.Http.Times[1] - rig.Http.Times[0] == TimeSpan.FromSeconds(60),
                "Cancellation at the response boundary discarded a received 429 cooldown.");
        logger.LogInformation("WeebCentral backoff harness passed: real proxy rotation, HTML/images, shared pacing, exponential delays, Retry-After, exhaustion, disposal and cancellation (no network/database access)");
    }

    /// <summary>Builds the production scheduler and proxy pool around controlled HTTP responses.</summary>
    private static Rig Create(Func<VirtualTime, int, Reply> response, int attempts = 4, int maximum = 300)
    {
        var clock = new VirtualTime();
        var http = DispatchProxy.Create<IHttpService, HttpStub>();
        var stub = (HttpStub)http;
        stub.Clock = clock;
        stub.Response = call => response(clock, call);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Proxies:Urls:0"] = FirstProxy, ["Proxies:Urls:1"] = SecondProxy,
            ["Proxies:Tokens"] = "1000", ["WeebCentral:MaxAttempts"] = attempts.ToString(CultureInfo.InvariantCulture),
            ["WeebCentral:MaxBackoffSeconds"] = maximum.ToString(CultureInfo.InvariantCulture)
        }).Build();
        var log = new ProxyLog();
        var proxy = new ProxiedHttpService(http, config, log);
        return new(new(proxy, config, NullLogger<WeebCentralRequestService>.Instance, clock), stub, log, clock);
    }

    /// <summary>Advances virtual timers while permitting asynchronous continuations to finish.</summary>
    private static async Task Complete(Task task, VirtualTime clock, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("WeebCentral virtual-time harness stalled.");
            clock.AdvanceNext();
            await Task.Delay(1, token);
        }
        await task;
    }

    /// <summary>Reports a failed harness expectation.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>The production request/proxy services and their observation points.</summary>
    private sealed record Rig(WeebCentralRequestService Requests, HttpStub Http, ProxyLog ProxyLog, VirtualTime Clock);

    /// <summary>A scripted status or transport failure with an optional server retry deadline.</summary>
    public sealed record Reply(HttpStatusCode? Status, RetryConditionHeaderValue? RetryAfter = null);

    /// <summary>Supplies response bodies to the actual proxy service and records virtual send times.</summary>
    public class HttpStub : DispatchProxy
    {
        /// <summary>The scheduler's virtual clock.</summary>
        public TimeProvider Clock { get; set; } = TimeProvider.System;
        /// <summary>Creates the scripted response for each actual proxy request.</summary>
        public Func<int, Reply> Response { get; set; } = _ => throw new NotSupportedException();
        /// <summary>Records when the scheduler allowed requests to reach the proxy.</summary>
        public List<DateTimeOffset> Times { get; } = [];
        /// <summary>Counts disposed response ownership markers.</summary>
        public int Disposed;

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IHttpService.Download) || args?.Length != 4)
                throw new InvalidOperationException("Expected a request from the rotating proxy service.");
            Times.Add(Clock.GetUtcNow());
            var reply = Response(Times.Count);
            var response = reply.Status is { } status ? new HttpResponseMessage(status) : null;
            if (response is not null) response.Headers.RetryAfter = reply.RetryAfter;
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("<img alt='Page 1' src='https://images.test/page.jpg'>"));
            var owned = new List<IDisposable> { stream, new OnDispose(() => Disposed++) };
            if (response is not null) owned.Add(response);
            return Task.FromResult(new DownloadResult(owned, (string)args[0]!, Error: reply.Status == HttpStatusCode.OK ? null : "Temporary failure",
                Response: response, Stream: reply.Status == HttpStatusCode.OK ? stream : null));
        }
    }

    /// <summary>Rejects direct or caller-supplied downloaders for provider images.</summary>
    private sealed class RejectDownload : IDownloadService
    {
        /// <inheritdoc />
        public Task<DownloadResult> Download(string url, Dictionary<string, string>? headers, CancellationToken token) =>
            throw new InvalidOperationException("WeebCentral bypassed its shared proxy scheduler.");
    }

    /// <summary>Tracks failed-response disposal before retry waits.</summary>
    private sealed class OnDispose(Action callback) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => callback();
    }

    /// <summary>Captures the endpoints selected by the real rotating proxy service.</summary>
    private sealed class ProxyLog : ILogger<ProxiedHttpService>
    {
        /// <summary>Endpoints reported by the production proxy service before each HTTP call.</summary>
        public List<string> Downloads { get; } = [];
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc />
        public bool IsEnabled(LogLevel level) => true;
        /// <inheritdoc />
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
            var values = fields.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (values.GetValueOrDefault("{OriginalFormat}") is string format && format.StartsWith("Downloading "))
                Downloads.Add((string)values["ProxyUrl"]!);
        }
    }

    /// <summary>A manually advanced clock for the one-shot timers used by Task.Delay.</summary>
    private sealed class VirtualTime : TimeProvider
    {
        private readonly object _sync = new();
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private readonly List<Timer> _timers = [];
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() { lock (_sync) return _now; }
        /// <inheritdoc />
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            lock (_sync) { _timers.Add(timer); timer.Change(dueTime, period); }
            return timer;
        }
        /// <summary>Fires the earliest active one-shot timer without waiting in real time.</summary>
        public void AdvanceNext()
        {
            Timer? timer;
            lock (_sync)
            {
                timer = _timers.Where(t => t.Due is not null).MinBy(t => t.Due);
                if (timer is null) return;
                _now = timer.Due!.Value;
                timer.Due = null;
            }
            timer.Fire();
        }
        /// <summary>One scheduled Task.Delay callback; cancellation clears its due time.</summary>
        private sealed class Timer(VirtualTime clock, TimerCallback callback, object? state) : ITimer
        {
            internal DateTimeOffset? Due;
            /// <inheritdoc />
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (period != Timeout.InfiniteTimeSpan) throw new NotSupportedException("Only one-shot harness timers are supported.");
                lock (clock._sync) Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
                return true;
            }
            /// <summary>Completes the associated Task.Delay at its virtual deadline.</summary>
            internal void Fire() => callback(state);
            /// <inheritdoc />
            public void Dispose() { lock (clock._sync) Due = null; }
            /// <inheritdoc />
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
