using MangaBox.Models.Composites.Import;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using MangaBox.Utilities.Comix;
using MangaBox.Utilities.Flare;
using MangaBox.Utilities.Flare.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs;

internal static class ComixChapterPageTests
{
    private const string ChapterUrl = "https://comix.to/title/68n8-mangatitle/1-chapter-19.1";
    private const string ImagePrefix = "bEqPbYfoKT0GmkHlA1afoD5Y4rUFcsai3R0Vvq7I6y4";
    private const string ImageSuffix = "S5FIHyE";

    public static async Task Run(CancellationToken token)
    {
        string[] hosts = ["rxn.meganwrites.site", "rxn.softlifejournal.site", "rxn.emilyscorner.site"];
        string[] tokens = ["Aj", "Ag", "Ah", "Am", "An", "Ak", "Al", "Aq", "Ar", "Ei", "Ej", "Eg"];
        var html = new StringBuilder("<img src='https://example.com/cover.webp'><button class='rpage-progress__seg' title='Page 12'></button>");
        for (var i = 0; i < tokens.Length; i++)
        {
            html.Append($"<div class='rpage-page' data-page='{i + 1}' style='aspect-ratio: 800 / 1599'>");
            if (i < 3)
                html.Append($"<img class='rpage-page__img' src='https://{hosts[i]}/hi/{ImagePrefix}{tokens[i]}{ImageSuffix}'>");
            html.Append("</div>");
        }

        var pages = await Parse(html.ToString(), token);
        Require(pages.Length == 12, "Expected all 12 reader pages, including lazy pages.");
        for (var i = 0; i < pages.Length; i++)
        {
            var expectedHost = hosts[i < 3 ? i : 0];
            Require(pages[i].Page == $"https://{expectedHost}/hi/{ImagePrefix}{tokens[i]}{ImageSuffix}",
                $"Unexpected URL for page {i + 1}.");
            Require(pages[i].Headers.Count == 1 && pages[i].Headers[0].Value == (i + 1).ToString(),
                "Plain CDN images must only carry the page ordinal.");
        }
        Require(pages[0].Width == 800 && pages[0].Height == 1599, "Reader dimensions were lost.");

        var initialData = JsonSerializer.Serialize(new { pages = pages.Take(3).Select(x => new { url = x.Page, width = x.Width, height = x.Height }) });
        var initialPages = await Parse($"<script id='initial-data' type='application/json'>{initialData}</script>", token);
        Require(initialPages.Length == 3 && initialPages.Select(x => x.Page).SequenceEqual(pages.Take(3).Select(x => x.Page)),
            "New CDN URLs were not parsed from the reader's initial data.");

        var source = new ComixSource(new HtmlStub(""), NullLogger<ComixSource>.Instance);
        var downloader = new DownloadStub();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["referer"] = "https://comix.to",
            ["origin"] = "https://comix.to",
            ["comix-fallback"] = "1"
        };
        using (await source.DownloadImage(downloader, pages[3].Page, headers, token))
        {
            Require(!downloader.Headers.ContainsKey("Referer") && !downloader.Headers.ContainsKey("Origin"),
                "Plain CDN requests must match the reader's no-referrer policy.");
            Require(!downloader.Headers.ContainsKey("comix-fallback"), "Internal flags were sent to the CDN.");
            Require(headers.ContainsKey("origin"), "Input headers were mutated.");
        }

        var legacyUrl = $"https://ek10.wowpic4.store/i5/{ImagePrefix}Am{ImageSuffix}";
        var legacy = await Parse($"<div class='rpage-page' data-page='4'><img class='rpage-page__img' src='{legacyUrl}'></div>", token);
        Require(legacy.Length == 1 && legacy[0].Page == legacyUrl + "#scrambled" &&
            legacy[0].Headers.Any(x => x.Name == "Origin"), "Legacy scrambled images changed.");
        using (await source.DownloadImage(downloader, legacy[0].Page, headers, token))
            Require(downloader.Headers.ContainsKey("Referer") && downloader.Headers.ContainsKey("Origin"),
                "Legacy CDN request headers changed.");

        var v3 = await Parse($"<div class='rpage-page' data-page='4'><img class='rpage-page__img' src='{legacyUrl}?v3'></div>", token);
        Require(v3.Length == 1 && v3[0].Headers.Any(x => x.Name == "comix-remove-origin") &&
            !v3[0].Page.Contains('#'), "Legacy v3 images changed.");
        var unrelated = await Parse("<div class='rpage-page' data-page='1'><img class='rpage-page__img' src='https://example.com/unrelated'></div>", token);
        Require(unrelated.Length == 0, "An unrelated extensionless URL was accepted.");

        foreach (var status in new[] { 429, 500, 502, 503, 504, 520, 521, 522, 523, 524 })
            Require(ComixSource.ShouldRetryImageResponse((HttpStatusCode)status), $"HTTP {status} should be retried.");
        foreach (var status in new[] { 200, 400, 401, 403, 404 })
            Require(!ComixSource.ShouldRetryImageResponse((HttpStatusCode)status), $"HTTP {status} should not be retried.");

        var recovering = new RetryDownloadStub([(HttpStatusCode)520, HttpStatusCode.OK]);
        using (var recovered = await source.DownloadImage(recovering, pages[0].Page, null, token))
        {
            Require(recovering.RequestCount == 2 && recovered.Response?.StatusCode == HttpStatusCode.OK,
                "A temporary 520 did not recover on retry.");
            Require(recovering.DisposedCount == 1, "The failed response was not disposed before retry.");
        }
        Require(recovering.DisposedCount == 2, "The successful response was not disposed by the caller.");

        var networkRecovery = new RetryDownloadStub([null, HttpStatusCode.OK]);
        using (var recovered = await source.DownloadImage(networkRecovery, pages[0].Page, null, token))
            Require(networkRecovery.RequestCount == 2 && recovered.Response?.StatusCode == HttpStatusCode.OK &&
                networkRecovery.DisposedCount == 1, "A transport failure did not recover on retry.");

        var missing = new RetryDownloadStub([HttpStatusCode.NotFound]);
        using (await source.DownloadImage(missing, pages[0].Page, null, token))
            Require(missing.RequestCount == 1, "A missing image was unnecessarily retried.");

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var interrupted = new RetryDownloadStub([(HttpStatusCode)520], cancelled.Cancel);
        try
        {
            using var unexpected = await source.DownloadImage(interrupted, pages[0].Page, null, cancelled.Token);
            throw new InvalidOperationException("Cancellation during retry backoff was ignored.");
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
        {
            Require(interrupted.RequestCount == 1 && interrupted.DisposedCount == 1,
                "Cancellation did not release the failed response.");
        }

        var proxy = new ProxyDownloadStub([HttpStatusCode.OK, HttpStatusCode.OK]);
        var internalHeaders = new Dictionary<string, string>
        {
            [IProxiedHttpService.AFFINITY_HEADER] = "same-manga",
            [IDownloadService.CHAPTER_URL_HEADER] = ChapterUrl,
            ["ordinal"] = "3"
        };
        using (await source.DownloadImage(proxy, pages[2].Page, internalHeaders, token))
            Require(proxy.AffinityRequests == 0, "Plain images must use normal proxy rotation instead of legacy affinity batches.");
        using (await source.DownloadImage(proxy, legacy[0].Page, internalHeaders, token))
            Require(proxy.AffinityRequests == 1 && proxy.LastAffinity == "same-manga",
                "Legacy images must retain their manga affinity.");
        Require(proxy.RequestHeaders.All(x => !x.ContainsKey(IDownloadService.CHAPTER_URL_HEADER) &&
            !x.ContainsKey(IProxiedHttpService.AFFINITY_HEADER)), "Internal chapter metadata was sent to a CDN.");

        var freshHtml = new HtmlStub(html.ToString());
        var freshSource = new ComixSource(freshHtml, NullLogger<ComixSource>.Instance);
        var staleHost = new RetryDownloadStub([null, null, HttpStatusCode.OK, HttpStatusCode.OK]);
        var recoveries = await Task.WhenAll(new[] { 2, 3 }.Select(page => freshSource.DownloadImage(
            staleHost, $"https://expired.example.site/hi/{ImagePrefix}{tokens[page - 1]}{ImageSuffix}",
            new Dictionary<string, string> { [IDownloadService.CHAPTER_URL_HEADER] = ChapterUrl, ["ordinal"] = page.ToString() }, token)));
        foreach (var recovery in recoveries) recovery.Dispose();
        Require(freshHtml.RequestCount == 1 && staleHost.RequestCount == 4,
            "Concurrent image failures must share one refreshed reader lookup.");
        Require(staleHost.Urls.Contains(pages[1].Page) && staleHost.Urls.Contains(pages[2].Page),
            "Refreshed URLs must preserve each requested page's ordinal.");
        Require(staleHost.DisposedCount == 4, "Refreshed image responses leaked their download resources.");

        var mirrors = new RetryDownloadStub([null, HttpStatusCode.OK]);
        using (await freshSource.DownloadImage(mirrors, pages[0].Page,
            new Dictionary<string, string> { [IDownloadService.CHAPTER_URL_HEADER] = ChapterUrl, ["ordinal"] = "1" }, token))
            Require(mirrors.Urls.Last() == $"https://{hosts[1]}/hi/{ImagePrefix}{tokens[0]}{ImageSuffix}",
                "An unchanged failing URL must try another CDN host advertised by the reader.");
        Require(freshHtml.RequestCount == 1, "Recent reader metadata should be reused for image recovery.");
    }

    private static Task<ImportPage[]> Parse(string html, CancellationToken token) =>
        new ComixSource(new HtmlStub(html), NullLogger<ComixSource>.Instance).ChapterPages(ChapterUrl, token);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class HtmlStub(string html) : IComixHtmlService
    {
        public int RequestCount { get; private set; }
        public Task<FlareHtmlDocument> GetHtml(string url, CancellationToken token)
        {
            RequestCount++;
            var document = new FlareHtmlDocument { FlareSolution = new SolverSolution { Url = url, Response = html } };
            document.LoadHtml(html);
            return Task.FromResult(document);
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DownloadStub : IDownloadService
    {
        public Dictionary<string, string> Headers { get; private set; } = [];

        public Task<DownloadResult> Download(string url, Dictionary<string, string>? headers, CancellationToken token)
        {
            Headers = headers ?? [];
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(new DownloadResult([response], url, headers, Response: response));
        }
    }

    private class RetryDownloadStub(HttpStatusCode?[] statuses, Action? afterRequest = null) : IDownloadService
    {
        private int _requests;
        private int _disposed;
        public int RequestCount => _requests;
        public int DisposedCount => _disposed;
        public ConcurrentQueue<string> Urls { get; } = new();
        public ConcurrentQueue<Dictionary<string, string>> RequestHeaders { get; } = new();

        public Task<DownloadResult> Download(string url, Dictionary<string, string>? headers, CancellationToken token)
        {
            var index = Interlocked.Increment(ref _requests) - 1;
            Urls.Enqueue(url);
            RequestHeaders.Enqueue(headers ?? []);
            var status = statuses[Math.Min(index, statuses.Length - 1)];
            var response = status is not null ? new HttpResponseMessage(status.Value) : null;
            var disposables = new List<IDisposable> { new DisposalCallback(() => Interlocked.Increment(ref _disposed)) };
            if (response is not null) disposables.Add(response);
            afterRequest?.Invoke();
            return Task.FromResult(new DownloadResult(disposables, url, headers,
                Error: status == HttpStatusCode.OK ? null : status is null ? "Connection reset" : $"HTTP {(int)status}",
                Response: response));
        }
    }

    private sealed class ProxyDownloadStub(HttpStatusCode?[] statuses) : RetryDownloadStub(statuses), IProxiedHttpService
    {
        public int AffinityRequests { get; private set; }
        public string? LastAffinity { get; private set; }
        public (string[] Urls, int Tokens, double Seconds) GetConfig() => ([], 10, 10);
        public Task<(ProxyEndpoint endpoint, System.Threading.RateLimiting.RateLimitLease lease)> Aquire(CancellationToken token) =>
            throw new NotSupportedException();
        public Task<DownloadResult> DownloadAffinitized(string workload, string affinity, string url,
            Dictionary<string, string>? headers, CancellationToken token)
        {
            AffinityRequests++;
            LastAffinity = affinity;
            return Download(url, headers, token);
        }
    }

    private sealed class DisposalCallback(Action callback) : IDisposable
    {
        public void Dispose() => callback();
    }
}
