using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Exercises the requested live series and the configured image proxy pool.</summary>
internal static class WeebCentralHarness
{
    /// <summary>Checks the exact image from the reported tunnel-reset log, or a caller-supplied image URL.</summary>
    public static async Task Image(IWeebCentralSource source, IProxiedHttpService proxy, ILogger logger, string? url, CancellationToken token)
    {
        url ??= "https://scans.lastation.us/manga/The-Transfer-Destination-Was-a-World-with-few-Pharmacists/0035-004.png";
        using var download = await source.DownloadImage(proxy, url,
            new() { ["Referer"] = source.HomeUrl + "/", ["User-Agent"] = source.UserAgent! }, token);
        if (download.Error is not null || download.Stream is null)
            throw new InvalidOperationException($"WeebCentral image failed after retry: {download.Error}");
        using var bitmap = SkiaSharp.SKBitmap.Decode(download.Stream);
        if (bitmap is null || bitmap.Width == 0 || bitmap.Height == 0) throw new InvalidDataException("Invalid image data.");
        logger.LogInformation("WeebCentral image decoded through rotating proxies: {Width}x{Height}, {Url} (no database writes)",
            bitmap.Width, bitmap.Height, url);
    }

    /// <summary>The series requested for import; shared by validation and the explicit import command.</summary>
    internal static readonly string[] SeriesUrls =
    [
        "https://weebcentral.com/series/01J76XYCCXY4XW7B6E7ZHX934M/Magi-Craft-Meister",
        "https://weebcentral.com/series/01J76XYGTGJ5S6YTDKKPVYHJVH/The-Transfer-Destination-Was-a-World-with-few-Pharmacists"
    ];

    /// <summary>
    /// Fetches complete chapter lists, checks first/latest reader pages, and decodes
    /// an image through the real proxy service. Nothing is loaded into the database.
    /// </summary>
    public static async Task Run(IWeebCentralSource source, IProxiedHttpService proxy, ILogger logger, CancellationToken token)
    {
        foreach (var url in SeriesUrls)
        {
            var (matches, id) = source.MatchesProvider(url);
            if (!matches || id is null || !source.UseProxiedImages)
                throw new InvalidOperationException("WeebCentral URL matching or proxy configuration failed.");
            if (source.MatchesProvider(url.Replace("weebcentral.com", "weebcentral.com.example.org")).matches)
                throw new InvalidOperationException("WeebCentral accepted an unrelated host.");
            var manga = await source.Manga(id, token)
                ?? throw new InvalidOperationException($"No metadata returned for {url}.");
            if (manga.Chapters.Count <= 9 || manga.Authors.Length == 0 || manga.Cover.Length == 0 || string.IsNullOrEmpty(manga.Description))
                throw new InvalidOperationException("Expected complete metadata and the full chapter list.");
            foreach (var chapter in new[] { manga.Chapters[0], manga.Chapters[^1] })
            {
                var pages = await source.ChapterPages(chapter.Url, token);
                if (pages.Length == 0 || pages.Any(page => !Uri.IsWellFormedUriString(page.Page, UriKind.Absolute)))
                    throw new InvalidOperationException($"No valid reader images for {chapter.Url}.");
                var headers = new Dictionary<string, string> { ["Referer"] = source.HomeUrl + "/", ["User-Agent"] = source.UserAgent! };
                using var download = await source.DownloadImage(proxy, pages[0].Page, headers, token);
                if (download.Error is not null || download.Stream is null)
                    throw new InvalidOperationException($"Proxied image download failed: {download.Error}");
                using var bitmap = SkiaSharp.SKBitmap.Decode(download.Stream);
                if (bitmap is null || bitmap.Width == 0) throw new InvalidDataException("The proxy did not return an image.");
                logger.LogInformation("WeebCentral {Title}, chapter {Chapter}: {Pages} pages; proxied image decoded", manga.Title, chapter.Number, pages.Length);
            }
            logger.LogInformation("WeebCentral passed: {Title}, {Chapters} chapters (no database writes)", manga.Title, manga.Chapters.Count);
        }
    }
}
