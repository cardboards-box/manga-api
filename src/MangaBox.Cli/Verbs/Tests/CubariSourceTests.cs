using CardboardBox.Json;
using MangaBox.Models.Composites.Import;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Read-only Cubari checks: no source lookup, loader, cache, or database services.</summary>
internal static class CubariSourceTests
{
    private const string FirstId = "cmF3L2JlZ2ExUS9UaGUtUG9pc29uLUtpbmctTWFuZ2EvbWFpbi9mYWxzZXdvcmxkLmpzb24";
    private const string SecondId = "cmF3L2l2YWs3NS9DdWJhcmkubWFuZ2EvbWFpbi8uMS5qc29u";
    private const string FixtureUrl = "https://cubari.moe/read/api/gist/series/TestSlug/";
    private const string ProxyUrl = "https://cubari.moe/proxy/api/imgchest/chapter/test";

    private const string Fixture = """
        {
          "title": "Fixture Manga", "description": "A description",
          "author": "Author", "artist": "Artist", "cover": "//images.example/cover.jpg",
          "groups": { "9": "Zulu", "2": "Alpha" },
          "chapters": {
            "10": { "title": "Tenth", "volume": "", "groups": { "9": "/proxy/api/imgchest/chapter/test" } },
            "1.5": { "title": "Extra", "volume": "2.5", "release_date": { "2": "1700000000" },
              "groups": { "9": ["https://images.example/wrong.jpg"],
                          "2": ["https://images.example/first.jpg?_w.", { "src": "https://images.example/second.jpg", "description": "Credits" }] } },
            "2": { "title": "Second", "volume": 3, "groups": { "2": ["https://images.example/two.jpg"] } },
            "3": { "groups": { "2": [] } }
          }
        }
        """;

    public static async Task RunParsing(CancellationToken token)
    {
        var factory = new ClientStub(new()
        {
            [FixtureUrl] = Fixture,
            [ProxyUrl] = """["https://images.example/proxy-first.jpg", { "src": "//images.example/proxy-second.jpg" }]"""
        });
        var api = new ApiService(factory, new SystemTextJsonService(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var source = new CubariSource(api, NullLogger<CubariSource>.Instance);
        foreach (var id in new[] { FirstId, SecondId, "TestSlug", "abc_-123=" })
        foreach (var suffix in new[] { "", "/", "/1-5/1/?foo=bar#page" })
        {
            var match = source.MatchesProvider($"https://cubari.moe/read/gist/{id}{suffix}");
            Require(match.matches && match.part == id, "Series/reader URL lost its case-sensitive gist ID.");
        }
        foreach (var url in new[]
        {
            "https://cubari.moe.evil.test/read/gist/TestSlug/", "https://evil.test/?cubari.moe/read/gist/TestSlug/",
            "https://cubari.moe/read/gist/", "https://cubari.moe/read/imgur/TestSlug/", "not a url",
            "ftp://cubari.moe/read/gist/TestSlug/", "https://cubari.moe:123/read/gist/TestSlug/",
            "https://user@cubari.moe/read/gist/TestSlug/", "https://cubari.moe/read/gist/%2Fbad/"
        }) Require(!source.MatchesProvider(url).matches, $"Unexpected URL match: {url}");

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var manga = await source.Manga("TestSlug", token);
            Require(manga is not null && manga.Id == "TestSlug" && manga.Provider == "cubari" &&
                manga.Title == "Fixture Manga" && manga.Description == "A description" &&
                manga.Authors.SequenceEqual(["Author"]) && manga.Artists.SequenceEqual(["Artist"]) &&
                manga.Cover.SequenceEqual(["https://images.example/cover.jpg"]), "Manga metadata was lost.");
            Require(manga!.Chapters.Select(c => c.Number).SequenceEqual([1.5, 2, 3, 10]),
                "Chapter numbers must sort numerically using invariant culture.");
            Require(manga.Chapters[0].Id == "1.5" && manga.Chapters[0].Volume == 2.5 &&
                manga.Chapters[1].Volume == 3 && manga.Chapters[^1].Volume is null,
                "Decimal chapters, string/numeric volumes, or empty volumes changed.");
            Require(manga.Chapters[0].Attributes.Any(a => a.Name == "Group" && a.Value == "Alpha") &&
                manga.Chapters[0].Attributes.Any(a => a.Name == "Release Date" && a.Value == "1700000000"),
                "Selected scanlation group or release date was lost.");
            var pages = await source.ChapterPages("TestSlug", "1.5", token);
            Require(pages.Select(p => p.Page).SequenceEqual([
                "https://images.example/first.jpg?_w.", "https://images.example/second.jpg"]),
                "Inline strings/objects, page order, or query strings changed.");
            var readerPages = await source.ChapterPages(manga.Chapters[0].Url, token);
            Require(readerPages.Select(p => p.Page).SequenceEqual(pages.Select(p => p.Page)),
                "Decimal reader URLs must resolve to the same pages as chapter IDs.");
            var proxyPages = await source.ChapterPages("TestSlug", "10", token);
            Require(proxyPages.Select(p => p.Page).SequenceEqual([
                "https://images.example/proxy-first.jpg", "https://images.example/proxy-second.jpg"]),
                "Proxy pages were not resolved in order.");
            Require((await source.ChapterPages("TestSlug", "missing", token)).Length == 0 &&
                (await source.ChapterPages("TestSlug", "3", token)).Length == 0 &&
                (await source.ChapterPages("https://evil.test/reader", token)).Length == 0,
                "Missing/empty chapters and unrelated URLs should return no pages.");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }

        // Group numeric IDs can be reassigned by Cubari on each request.
        factory.Responses[FixtureUrl] = Fixture.Replace("\"9\"", "\"new-z\"").Replace("\"2\": \"Alpha\"", "\"new-a\": \"Alpha\"")
            .Replace("\"2\": [", "\"new-a\": [").Replace("\"2\": \"1700000000\"", "\"new-a\": \"1700000000\"");
        Require((await source.ChapterPages("TestSlug", "1.5", token))[0].Page == "https://images.example/first.jpg?_w.",
            "Changing group IDs changed the selected translation.");

        foreach (var body in new[]
        {
            "not json", "null", "{}", "{\"title\":\"Missing chapters\"}",
            "{\"title\":\"Invalid chapter\",\"chapters\":{\"1\":null}}"
        })
        {
            factory.Responses[FixtureUrl] = body;
            Require(await source.Manga("TestSlug", token) is null, "Invalid API metadata should fail cleanly.");
        }
        factory.Responses.Remove(FixtureUrl);
        Require(await source.Manga("TestSlug", token) is null, "An HTTP 404 should fail cleanly.");
        factory.Responses[FixtureUrl] = Fixture;
        foreach (var body in new[] { "{}", "[\"https://images.example/valid.jpg\", null]", "[\"javascript:bad\"]" })
        {
            factory.Responses[ProxyUrl] = body;
            Require((await source.ChapterPages("TestSlug", "10", token)).Length == 0,
                "Invalid proxy responses must not return partial chapters.");
        }
        factory.Responses.Remove(ProxyUrl);
        Require((await source.ChapterPages("TestSlug", "10", token)).Length == 0,
            "A failed proxy must not return pages.");
        factory.Responses[FixtureUrl] = Fixture.Replace("/proxy/api/imgchest/chapter/test", "https://evil.test/proxy/api/test");
        Require((await source.ChapterPages("TestSlug", "10", token)).Length == 0 &&
            factory.Requests.All(u => new Uri(u).Host == "cubari.moe"), "An external JSON proxy was fetched.");

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancelled.Cancel();
        try
        {
            await source.Manga("TestSlug", cancelled.Token);
            throw new InvalidOperationException("Cubari ignored cancellation.");
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
    }

    public static async Task RunLive(ICubariSource source, IHttpService http, ILogger logger, CancellationToken token)
    {
        // Deliberately bypass SourceService.FindByUrl and MangaLoaderService:
        // source discovery itself can upsert source records in the database.
        foreach (var id in new[] { FirstId, SecondId })
        {
            var url = $"https://cubari.moe/read/gist/{id}/";
            var match = source.MatchesProvider(url);
            Require(match.matches && match.part == id, "The example Cubari URL was not recognized.");
            var manga = await source.Manga(id, token);
            Require(manga is not null && !string.IsNullOrWhiteSpace(manga.Title) && manga.Chapters.Count > 0,
                $"No metadata/chapters returned for {url}");
            Require(manga!.Provider == source.Provider && manga.Id == id && manga.HomePage == url,
                "Imported manga identity did not round-trip.");
            Require(manga.Chapters.Select(c => c.Id).Distinct().Count() == manga.Chapters.Count &&
                manga.Chapters.Select(c => c.Number).SequenceEqual(manga.Chapters.Select(c => c.Number).Order()),
                "Live chapter IDs must be unique and chapters must be sorted.");
            var totalPages = 0;
            foreach (var chapter in manga.Chapters)
            {
                var pages = await source.ChapterPages(id, chapter.Id, token);
                Require(pages.Length > 0 && pages.All(p => IsImageUrl(p.Page)),
                    $"No valid pages for {chapter.Url}");
                totalPages += pages.Length;
                // Download and decode a sample from every chapter, keeping all data in memory.
                await CheckImage(source, http, pages[0], token);
                logger.LogInformation("Cubari chapter {Chapter}: {PageCount} pages, sample image decoded", chapter.Id, pages.Length);
            }
            Require(manga.Cover.Length > 0, $"No cover returned for {url}");
            await CheckImage(source, http, new ImportPage(manga.Cover[0]), token);
            logger.LogInformation("Cubari passed: {Title}; {Chapters} chapters, {Pages} pages, cover decoded (no database access)",
                manga.Title, manga.Chapters.Count, totalPages);
        }
    }

    private static bool IsImageUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static async Task CheckImage(ICubariSource source, IHttpService http, ImportPage page, CancellationToken token)
    {
        var headers = new Dictionary<string, string>(source.Headers ?? [], StringComparer.OrdinalIgnoreCase);
        if (source.Referer is { Length: > 0 } referer) headers["Referer"] = referer;
        if (source.UserAgent is { Length: > 0 } userAgent) headers["User-Agent"] = userAgent;
        foreach (var header in page.Headers) headers[header.Name] = header.Value;
        using var result = await source.DownloadImage(http, page.Page, headers, token);
        Require(string.IsNullOrEmpty(result.Error) && result.Response?.IsSuccessStatusCode == true && result.Stream is not null,
            $"Image download failed: {page.Page}: {result.Error}");
        using var image = SkiaSharp.SKBitmap.Decode(result.Stream);
        Require(image is not null && image.Width > 0 && image.Height > 0, $"Invalid image data: {page.Page}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ClientStub(Dictionary<string, string> responses) : IHttpClientFactory
    {
        public Dictionary<string, string> Responses { get; } = responses;
        public List<string> Requests { get; } = [];
        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(ClientStub owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var url = request.RequestUri!.AbsoluteUri;
                owner.Requests.Add(url);
                var found = owner.Responses.TryGetValue(url, out var body);
                return Task.FromResult(new HttpResponseMessage(found ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                {
                    Content = new StringContent(body ?? "Not found", Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
