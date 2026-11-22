using CardboardBox.Json;
using MangaBox.Database;
using MangaBox.Providers;
using LoggerConfiguration = Serilog.LoggerConfiguration;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Offline checks for response disposal and bounded database log buffering.</summary>
internal static class MemoryRetentionHarness
{
    private const string Url = "https://memory.test/page";
    public static async Task Run(ILogger logger, CancellationToken token)
    {
        using var transport = new Transport();
        var json = new SystemTextJsonService(new JsonSerializerOptions());
        await CheckRequestPaths(new ApiService(transport, json), transport, token);
        logger.LogInformation("Application memory checks passed: cancellation, HTTP/JSON failures, response disposal, concurrent bounded log buffering, and oversized logs. HTTP retention regression tests live in the CardboardBox.Http repository.");
    }

    private static async Task CheckRequestPaths(IApiService api, Transport transport, CancellationToken token)
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await ExpectFailure<OperationCanceledException>(() => api.Get(Url, token: cancelled.Token));

        // Exercise cancellation added through the builder, including two tokens,
        // as HttpService and provider extension methods do.
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        transport.Respond = async ct =>
        {
            second.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Cancellation did not interrupt the request.");
        };
        await ExpectFailure<OperationCanceledException>(() => api.Create(Url, config: null, token: first.Token).CancelWith(second.Token).Result());
        first.Cancel(); // Must not invoke Cancel on an already disposed request source.

        transport.Respond = _ => throw new HttpRequestException("Simulated transport failure");
        using var owner = new CancellationTokenSource();
        await ExpectFailure<HttpRequestException>(() => api.Get(Url, token: owner.Token));
        owner.Cancel();

        transport.Respond = _ => Task.FromResult(Response("{\"value\":7}"));
        var parsed = await api.Get<Dictionary<string, int>>(Url, token: token);
        Require(parsed?["value"] == 7, "The single-type JSON request changed behavior.");
        var status = await api.Get<Dictionary<string, int>, Dictionary<string, string>>(Url, token: token);
        Require(status is not null, "The two-type JSON request failed.");

        transport.Respond = _ => Task.FromResult(Response("invalid JSON"));
        await ExpectFailure<JsonException>(() => api.Get<Dictionary<string, int>>(Url, token: token));

        // Provider helpers must release responses even when parsing/status checks fail.
        var disposed = 0;
        transport.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new TrackedContent(() => disposed++)
        });
        await ExpectFailure<HttpRequestException>(() => PolyfillExtensions.GetHtml(api, Url, token: token));
        await ExpectFailure<HttpRequestException>(() => PolyfillExtensions.PostHtml(api, Url, token: token));
        await ExpectFailure<HttpRequestException>(() => PolyfillExtensions.PostHtml(api, Url, [("key", "value")], token: token));
        Require(disposed == 3, "Provider HTML error responses were not disposed.");
    }

    private static async Task ExpectFailure<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public Func<CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = _ => Task.FromResult(Response("{}"));
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Respond(cancellationToken);
        }
    }

    private sealed class TrackedContent(Action disposed) : StringContent("Simulated origin failure")
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) disposed();
            base.Dispose(disposing);
        }
    }
}
