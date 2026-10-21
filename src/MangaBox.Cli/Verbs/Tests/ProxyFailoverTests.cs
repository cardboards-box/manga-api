using System.Net.Sockets;
using MangaBox.Services.Imaging;

namespace MangaBox.Cli.Verbs.Tests;

internal static class ProxyFailoverTests
{
    private const string Url = "https://example.test/image.webp";
    private const string FirstProxy = "socks5://127.0.0.1:1080/";
    private const string SecondProxy = "socks5://127.0.0.1:1081/";

    public static async Task Run(IHttpService http, CancellationToken token)
    {
        await CheckTunnelFailureClassification(http, token);
        foreach (var error in new[] { HttpRequestError.ProxyTunnelError, HttpRequestError.ConnectionError })
            await CheckFailover(error, token);
        await CheckQueuedRequests(token);
        await CheckRateLimitAffinity(token);
        await CheckCancellation(token);
        await CheckOriginError(token);
        await CheckAllUnavailable(token);
        foreach (var error in new[] { HttpRequestError.ProxyTunnelError, HttpRequestError.ConnectionError })
            await CheckRotatingFailover(error, token);
        await CheckRotatingUnavailable(token);
        await CheckRotatingRecovery(token);
        await CheckRotatingCancellation(token);
        await CheckRotatingOriginError(token);
    }

    /// <summary>A broken tunnel is skipped by subsequent rotating calls until it recovers.</summary>
    private static async Task CheckRotatingFailover(HttpRequestError error, CancellationToken token)
    {
        var (service, log) = Create(call => Task.FromResult(call == 1 ? Failure(error) : Success()));
        for (var i = 0; i < 3; i++) using (await service.Download(Url, null, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, SecondProxy]),
            "Normal rotation reused a failed tunnel instead of the healthy endpoint.");
    }

    /// <summary>An unavailable rotating pool waits cancellably instead of hammering broken endpoints.</summary>
    private static async Task CheckRotatingUnavailable(CancellationToken token)
    {
        var (service, log) = Create(_ => Task.FromResult(Failure(HttpRequestError.ProxyTunnelError)));
        using (await service.Download(Url, null, token)) { }
        using (await service.Download(Url, null, token)) { }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        await ExpectCancellation(() => service.Download(Url, null, timeout.Token));
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy]), "Unavailable rotating endpoints were repeatedly contacted.");
    }

    /// <summary>Recovered proxies return to rotation after the configured connection cooldown.</summary>
    private static async Task CheckRotatingRecovery(CancellationToken token)
    {
        var (service, log) = Create(call => Task.FromResult(call == 1 ? Failure(HttpRequestError.ProxyTunnelError) : Success()), 0.1);
        using (await service.Download(Url, null, token)) { }
        await Task.Delay(TimeSpan.FromMilliseconds(150), token);
        using (await service.Download(Url, null, token)) { }
        using (await service.Download(Url, null, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, FirstProxy]), "A recovered proxy never reentered rotation.");
    }

    /// <summary>Caller cancellation cannot quarantine a healthy rotating endpoint.</summary>
    private static async Task CheckRotatingCancellation(CancellationToken token)
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var (service, log) = Create(call =>
        {
            if (call != 1) return Task.FromResult(Success());
            cancelled.Cancel();
            return Task.FromResult(Failure(HttpRequestError.ConnectionError));
        });
        using (await service.Download(Url, null, cancelled.Token)) { }
        using (await service.Download(Url, null, token)) { }
        using (await service.Download(Url, null, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, FirstProxy]), "Cancellation quarantined a healthy rotating endpoint.");
    }

    /// <summary>Origin HTTP failures are handled by providers and do not quarantine working tunnels.</summary>
    private static async Task CheckRotatingOriginError(CancellationToken token)
    {
        var (service, log) = Create(call => Task.FromResult(Success(call == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)));
        for (var i = 0; i < 3; i++) using (await service.Download(Url, null, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, FirstProxy]), "An origin response was misclassified as a broken tunnel.");
    }

    private static async Task CheckTunnelFailureClassification(IHttpService http, CancellationToken token)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var reset = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(token);
            socket.Client.LingerState = new LingerOption(true, 0);
        }, token);
        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"socks5://127.0.0.1:{port}"),
            UseProxy = true
        };
        using var result = await http.Download(Url, null,
            request => request.ClientFactory(_ => new HttpClient(handler, false)), token);
        await reset;
        Require(result.Response is null && result.RequestError == HttpRequestError.ProxyTunnelError,
            "A reset SOCKS tunnel must retain its typed connection error.");
    }

    private static async Task CheckFailover(HttpRequestError error, CancellationToken token)
    {
        var (service, log) = Create(call => Task.FromResult(call == 1 ? Failure(error) : Success()));
        using var first = await Download(service, token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var second = await Download(service, timeout.Token);
        second.Dispose();
        first.Dispose();
        using var third = await Download(service, timeout.Token);
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, SecondProxy]),
            "A connection failure must switch proxies and disposing the old lease must preserve the replacement affinity.");
    }

    private static async Task CheckQueuedRequests(CancellationToken token)
    {
        var response = new TaskCompletionSource<DownloadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, log) = Create(call => call == 1 ? response.Task : Task.FromResult(Success()));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var firstTask = Download(service, timeout.Token);
        var queuedTask = Download(service, timeout.Token);
        Require(!queuedTask.IsCompleted && log.Downloads.Count == 1,
            "The second request should wait behind the active request on the same proxy.");
        response.SetResult(Failure(HttpRequestError.ProxyTunnelError));
        using var first = await firstTask;
        using var replacement = await Download(service, timeout.Token);
        first.Dispose();
        replacement.Dispose();
        using var queued = await queuedTask;
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy, SecondProxy]),
            "Requests queued on a failed proxy must move to the replacement instead of contacting the failed tunnel.");
    }

    private static async Task CheckRateLimitAffinity(CancellationToken token)
    {
        var (service, log) = Create(_ => Task.FromResult(Success(HttpStatusCode.TooManyRequests)));
        using (await Download(service, token)) { }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        await ExpectCancellation(() => Download(service, timeout.Token));
        Require(log.Downloads.SequenceEqual([FirstProxy]),
            "Rate limits must preserve affinity and wait for the existing cooldown.");
    }

    private static async Task CheckCancellation(CancellationToken token)
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var (service, log) = Create(call =>
        {
            if (call == 1)
            {
                cancelled.Cancel();
                return Task.FromResult(Failure(HttpRequestError.ConnectionError));
            }
            return Task.FromResult(Success());
        });
        using (await Download(service, cancelled.Token)) { }
        using (await Download(service, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, FirstProxy]),
            "A cancelled request must not quarantine a healthy proxy.");
    }

    private static async Task CheckOriginError(CancellationToken token)
    {
        var (service, log) = Create(call => Task.FromResult(call == 1 ? Success((HttpStatusCode)520) : Success()));
        using (await Download(service, token)) { }
        using (await Download(service, token)) { }
        Require(log.Downloads.SequenceEqual([FirstProxy, FirstProxy]),
            "An HTTP response from the image host must not be classified as a failed proxy connection.");
    }

    private static async Task CheckAllUnavailable(CancellationToken token)
    {
        var (service, log) = Create(_ => Task.FromResult(Failure(HttpRequestError.ProxyTunnelError)));
        using (await Download(service, token)) { }
        using (await Download(service, token)) { }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        await ExpectCancellation(() => Download(service, timeout.Token));
        Require(log.Downloads.SequenceEqual([FirstProxy, SecondProxy]),
            "When every proxy is unavailable, acquisition must wait cancellably without repeatedly contacting them.");
    }

    private static Task<DownloadResult> Download(ProxiedHttpService service, CancellationToken token) =>
        service.DownloadAffinitized("Comix", "same-manga", Url, null, token);

    private static DownloadResult Failure(HttpRequestError error) =>
        new([], Url, Error: "Proxy connection failed") { RequestError = error };

    private static DownloadResult Success(HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status);
        return new([response], Url, Response: response);
    }

    private static (ProxiedHttpService Service, ProxyLog Log) Create(Func<int, Task<DownloadResult>> response, double cooldown = 60)
    {
        var http = DispatchProxy.Create<IHttpService, HttpStub>();
        ((HttpStub)http).Response = response;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Proxies:Urls:0"] = FirstProxy,
            ["Proxies:Urls:1"] = SecondProxy,
            ["Proxies:FailureCooldownSeconds"] = cooldown.ToString(CultureInfo.InvariantCulture),
            ["Proxies:Affinity:Comix:RequestSpacingSeconds"] = "0.1",
            ["Proxies:Affinity:Comix:IdleSeconds"] = "30",
            ["Proxies:Affinity:Comix:BatchSize"] = "20",
            ["Proxies:Affinity:Comix:BatchWindowSeconds"] = "0.1"
        }).Build();
        var log = new ProxyLog();
        return (new ProxiedHttpService(http, config, log), log);
    }

    private static async Task ExpectCancellation(Func<Task<DownloadResult>> action)
    {
        try
        {
            using var result = await action();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        throw new InvalidOperationException("Expected cancellation while waiting for an unavailable proxy.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public class HttpStub : DispatchProxy
    {
        public Func<int, Task<DownloadResult>> Response { get; set; } = _ => throw new NotSupportedException();
        private int _calls;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IHttpService.Download) || args?.Length != 4)
                throw new NotSupportedException(targetMethod?.Name);
            return Response(Interlocked.Increment(ref _calls));
        }
    }

    private sealed class ProxyLog : ILogger<ProxiedHttpService>
    {
        public List<string> Downloads { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
            var values = fields.ToDictionary(x => x.Key, x => x.Value);
            if (values.GetValueOrDefault("{OriginalFormat}") is string format && format.StartsWith("Downloading "))
                Downloads.Add((string)values["ProxyUrl"]!);
        }
    }
}
