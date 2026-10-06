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
    }

    private static Task<ImportPage[]> Parse(string html, CancellationToken token) =>
        new ComixSource(new HtmlStub(html), NullLogger<ComixSource>.Instance).ChapterPages(ChapterUrl, token);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class HtmlStub(string html) : IComixHtmlService
    {
        public Task<FlareHtmlDocument> GetHtml(string url, CancellationToken token)
        {
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
}
