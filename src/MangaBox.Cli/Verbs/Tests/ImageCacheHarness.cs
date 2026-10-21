using System.IO.Compression;
using CardboardBox.Json;
using MangaBox.Database;
using MangaBox.Database.Services;
using MangaBox.Models;
using MangaBox.Providers.Sources;
using MangaBox.Services;
using MangaBox.Services.Imaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>
/// Exercises the production cache, image, and ZIP services with isolated roots.
/// Network responses are supplied in memory and database access is rejected.
/// </summary>
internal static class ImageCacheHarness
{
    /// <summary>Runs the filesystem scenarios and removes only its own verified scratch directory.</summary>
    public static async Task Run(ILogger logger, CancellationToken token)
    {
        var parent = Path.GetFullPath("temp-env");
        var sandbox = Path.Combine(parent, "cache-harness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try { await CheckRoots(sandbox, logger, token); }
        finally
        {
            GuardPath(parent, sandbox);
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>Checks root order, mixed formats, metadata pairing, relocation, and primary-only writes.</summary>
    private static async Task CheckRoots(string sandbox, ILogger logger, CancellationToken token)
    {
        var primary = Path.Combine(sandbox, "primary cache");
        var first = Path.Combine(sandbox, "archive one");
        var second = Path.Combine(sandbox, "archive two");
        var missing = Path.Combine(sandbox, "unmounted");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Imaging:CacheDir"] = primary,
            ["Imaging:AdditionalCacheDirs:0"] = first,
            ["Imaging:AdditionalCacheDirs:1"] = second,
            ["Imaging:AdditionalCacheDirs:2"] = primary + Path.DirectorySeparatorChar,
            ["Imaging:AdditionalCacheDirs:3"] = missing
        }).Build();
        var json = new SystemTextJsonService(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var cache = new CacheService(json, config, NullLogger<CacheService>.Instance);
        Require(cache.StoragePaths.SequenceEqual([primary, first, second, missing]), "Roots must preserve priority and remove duplicates.");
        var transport = new Transport();
        var http = new HttpService(new ApiService(transport, json), json, NullLogger<HttpService>.Instance);
        var zip = new ZipService(http, cache, NullLogger<ZipService>.Instance);
        var images = new ImageService(Forbidden<IDbService>(), zip, http, cache, config,
            Forbidden<ISourceService>(), Forbidden<IFlareImageService>(), Forbidden<IProxiedHttpService>(),
            Forbidden<IMangaLoaderService>(), Forbidden<IRestitcherService>(), Forbidden<IProxiedFlareImageService>(),
            NullLogger<ImageService>.Instance);
        var image = new MbImage
        {
            Id = Guid.NewGuid(), MangaId = Guid.NewGuid(), ChapterId = Guid.NewGuid(),
            Url = "https://cache.test/page.jpg", FileName = "page.jpg", MimeType = "image/jpeg"
        };
        var paths = cache.GetImageCachePaths(image).ToArray();
        Require(!Directory.Exists(primary) && !Directory.Exists(first), "Enumerating read candidates created directories.");
        var write = cache.GetCachePath(image, out var hash, false);
        Require(write == paths[0] && Path.GetRelativePath(primary, write) ==
            Path.Combine(image.MangaId.ToString(), "pages", hash + ".dat"), "The existing relative image key changed.");
        await Write(paths[0], "primary raw", false, token);
        await Write(paths[2], "first raw", false, token);
        await Write(paths[3], "first compressed", true, token);
        await Write(paths[5], "second compressed", true, token);
        await ReadImage(images, image, "primary raw", token);
        File.Delete(paths[0]);
        await Write(paths[1], "primary compressed", true, token);
        await ReadImage(images, image, "primary compressed", token);
        File.Delete(paths[1]);
        await ReadImage(images, image, "first raw", token);
        File.Delete(paths[2]);
        await ReadImage(images, image, "first compressed", token);
        File.Delete(paths[3]);
        await ReadImage(images, image, "second compressed", token);

        var cover = new MbImage { MangaId = image.MangaId, Url = "https://cache.test/cover.jpg", FileName = "cover.jpg", MimeType = "image/jpeg" };
        var coverPath = cache.GetImageCachePaths(cover).ElementAt(4);
        await Write(coverPath, "archived cover", false, token);
        await ReadImage(images, cover, "archived cover", token);

        // The same service must rediscover files after whole manga directories move.
        File.Delete(paths[5]);
        await Write(paths[2], "relocated manga", false, token);
        var from = Path.Combine(first, image.MangaId.ToString());
        var to = Path.Combine(second, image.MangaId.ToString());
        GuardPath(sandbox, from);
        GuardPath(sandbox, to);
        Directory.Delete(to, true);
        Directory.Move(from, to);
        await ReadImage(images, image, "relocated manga", token);
        cache.StoragePath = second;
        Require(cache.GetCachePath(image, out var movedHash, false) == paths[4] && movedHash == hash,
            "Changing the primary changed the relative cache key.");
        cache.StoragePath = primary;

        const string externalUrl = "https://cache.test/external.jpg";
        var externalPaths = cache.GetExternalCachePaths(externalUrl).ToArray();
        await Write(externalPaths[0], "incomplete primary", false, token);
        await Write(externalPaths[1], "external archive", false, token);
        await cache.SaveMeta(new("archive.jpg", "image/jpeg", DateTime.UtcNow), externalPaths[1], token);
        var external = await images.Download(externalUrl, token);
        using (external.Stream)
            Require(external.Error is null && await Read(external.Stream!, token) == "external archive" && external.FileName == "archive.jpg",
                "External image data and metadata must come from the same root.");

        const string zipUrl = "https://cache.test/archive.zip";
        var zipPaths = cache.GetZipCachePaths(zipUrl).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(zipPaths[1])!);
        await File.WriteAllBytesAsync(zipPaths[1], Transport.ZipBytes(), token);
        var archivedZip = await zip.DownloadZip(zipUrl, null, token);
        Require(archivedZip.Path == zipPaths[1] && archivedZip.Files.SequenceEqual(["page.txt"]), "Archived ZIP was missed.");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPaths[2])!);
        GuardPath(sandbox, zipPaths[1]);
        GuardPath(sandbox, zipPaths[2]);
        File.Move(zipPaths[1], zipPaths[2]);
        Require((await zip.DownloadZip(zipUrl, null, token)).Path == zipPaths[2], "A completed ZIP task retained a stale physical path.");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPaths[0])!);
        await File.WriteAllBytesAsync(zipPaths[0], Transport.ZipBytes(), token);
        Require((await zip.DownloadZip(zipUrl, null, token)).Path == zipPaths[0], "Primary ZIP did not override an archive.");
        Require(transport.Requests == 0, "A cached read unexpectedly downloaded a file.");

        const string newExternal = "https://cache.test/new.jpg";
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => images.Download(newExternal, token)));
        foreach (var result in concurrent)
        {
            using (result.Stream)
                Require(result.Error is null && await Read(result.Stream!, token) == "downloaded image", "External cache miss failed.");
        }
        Require(transport.Requests == 1 && File.Exists(cache.GetCachePath(newExternal, out _)) &&
            File.Exists(Path.ChangeExtension(cache.GetCachePath(newExternal, out _), "json")) &&
            cache.GetExternalCachePaths(newExternal).Skip(1).All(path => !File.Exists(path)),
            "External misses must download once and save data/metadata only in the primary.");
        const string newZip = "https://cache.test/new.zip";
        Require((await zip.DownloadZip(newZip, null, token)).Path == cache.GetZipCachePath(newZip, out _) &&
            cache.GetZipCachePaths(newZip).Skip(1).All(path => !File.Exists(path)), "New ZIP was saved outside the primary.");

        // Exercise actual page downloads/compression with metadata updates kept in
        // memory. Unexpected database operations still fail immediately.
        var imageDb = Controlled<IMbImageDbService>((method, _) => method.Name == "Update"
            ? Task.FromResult(1) : throw new InvalidOperationException("Unexpected image database operation."));
        var database = Controlled<IDbService>((method, _) => method.Name == "get_Image"
            ? imageDb : throw new InvalidOperationException("Unexpected database service."));
        var provider = new CubariSource(new ApiService(transport, json), NullLogger<CubariSource>.Instance);
        var sources = Controlled<ISourceService>((method, _) => method.Name == "FindById"
            ? Task.FromResult<LoaderSource?>(new(new MbSource(), provider))
            : throw new InvalidOperationException("Unexpected source lookup."));
        var downloading = new ImageService(database, zip, http, cache, config, sources,
            Forbidden<IFlareImageService>(), Forbidden<IProxiedHttpService>(), Forbidden<IMangaLoaderService>(),
            Forbidden<IRestitcherService>(), Forbidden<IProxiedFlareImageService>(), NullLogger<ImageService>.Instance);
        foreach (var compressed in new[] { true, false })
        {
            config["Imaging:UseCompression"] = compressed.ToString();
            var page = new MbImage
            {
                Id = Guid.NewGuid(), MangaId = Guid.NewGuid(), ChapterId = Guid.NewGuid(),
                Url = $"https://cache.test/new-page.png?compressed={compressed}"
            };
            using (var result = await downloading.Get(new MbSource(), new MbManga(), page, null, token))
            {
                Require(result.Error is null && !result.FromCache && result.Stream is not null, "A new page download failed.");
                using var bitmap = SkiaSharp.SKBitmap.Decode(result.Stream);
                Require(bitmap?.Width == 2 && bitmap.Height == 3, "The downloaded/compressed image could not be decoded.");
            }
            Require(File.Exists(cache.GetCachePath(page, out _, compressed)) &&
                cache.GetImageCachePaths(page).Skip(2).All(path => !File.Exists(path)),
                "A new raw/compressed page was written outside the primary.");
            using var cachedPage = await downloading.Get(new MbSource(), new MbManga(), page, null, token);
            Require(cachedPage.Error is null && cachedPage.FromCache, "The downloaded page was not cached.");
        }

        // Invalidation waits for readers, then removes every format in every root.
        foreach (var path in paths.Take(6)) await Write(path, "old copy", path.EndsWith(".gz"), token);
        var open = await images.Get(new MbSource(), new MbManga(), image, null, token);
        image.FileName = null;
        image.MimeType = null;
        var remove = images.RemoveCachedFiles(image, token);
        Require(!remove.IsCompleted, "Invalidation must wait for an open response stream.");
        open.Dispose();
        await remove;
        Require(paths.All(path => !File.Exists(path)), "An old cache copy survived invalidation.");
        Require(!Directory.Exists(missing), "A missing secondary root was created.");
        var legacy = new CacheService(json, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Imaging:CacheDir"] = primary }).Build(), NullLogger<CacheService>.Instance);
        Require(legacy.StoragePaths.Count == 1 && legacy.GetCachePath(image, out _, false) == write,
            "Single-root configuration is no longer compatible.");
        logger.LogInformation("Image cache harness passed: primary/fallback order, raw/gzip, covers, relocation, external metadata, ZIPs, concurrent misses and invalidation; no database or network access");
    }

    /// <summary>Reads through the production image service; forbidden dependencies enforce a cache hit.</summary>
    private static async Task ReadImage(ImageService service, MbImage image, string expected, CancellationToken token)
    {
        using var result = await service.Get(new MbSource(), new MbManga(), image, null, token);
        Require(result.Error is null && result.FromCache && result.Stream is not null && await Read(result.Stream, token) == expected,
            $"Cache returned the wrong copy; expected {expected}.");
    }

    /// <summary>Reads a small harness marker and leaves stream ownership with the caller.</summary>
    private static async Task<string> Read(Stream stream, CancellationToken token)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(token);
    }

    /// <summary>Writes a raw or compressed marker into a harness fixture location.</summary>
    private static async Task Write(string path, string value, bool compressed, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        if (!compressed) { await file.WriteAsync(Encoding.UTF8.GetBytes(value), token); return; }
        await using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        await gzip.WriteAsync(Encoding.UTF8.GetBytes(value), token);
    }

    /// <summary>Rejects cleanup/move targets outside the explicitly owned scratch directory.</summary>
    private static void GuardPath(string root, string target) => Require(Path.GetFullPath(target).StartsWith(
        Path.GetFullPath(root) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
        "Harness filesystem operation escaped its scratch directory.");

    /// <summary>Fails the harness with a useful error.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Creates a dependency which rejects unexpected database or source access.</summary>
    private static T Forbidden<T>() where T : class => DispatchProxy.Create<T, RejectCalls>();

    /// <summary>Supplies a narrowly controlled in-memory dependency without contacting a database.</summary>
    private static T Controlled<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, RejectCalls>();
        ((RejectCalls)(object)proxy).Handler = handler;
        return proxy;
    }

    /// <summary>Rejects all calls on dependencies that cache hits must not use.</summary>
    public class RejectCalls : DispatchProxy
    {
        /// <summary>Optional in-memory behavior for explicitly allowed operations.</summary>
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod is not null && Handler is not null ? Handler(targetMethod, args) :
                throw new InvalidOperationException($"Unexpected dependency access: {targetMethod?.Name}");
    }

    /// <summary>Supplies deterministic HTTP responses to the real IApiService and download service.</summary>
    private sealed class Transport : IHttpClientFactory
    {
        /// <summary>The number of actual HTTP requests issued by the production services.</summary>
        public int Requests;
        /// <inheritdoc />
        public HttpClient CreateClient(string name) => new(new Handler(this));

        /// <summary>Builds a ZIP fixture without filesystem or network access.</summary>
        public static byte[] ZipBytes()
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
            using (var writer = new StreamWriter(archive.CreateEntry("page.txt").Open())) writer.Write("zip page");
            return memory.ToArray();
        }

        /// <summary>Encodes a small valid image for the real page download/compression pipeline.</summary>
        private static byte[] ImageBytes()
        {
            using var bitmap = new SkiaSharp.SKBitmap(2, 3);
            bitmap.Erase(SkiaSharp.SKColors.Green);
            using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        /// <summary>Returns in-memory image or ZIP bytes for controlled cache misses.</summary>
        private sealed class Handler(Transport owner) : HttpMessageHandler
        {
            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Interlocked.Increment(ref owner.Requests);
                var zip = request.RequestUri!.AbsolutePath.EndsWith(".zip");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zip ? ZipBytes() : request.RequestUri.AbsolutePath == "/new-page.png"
                        ? ImageBytes() : Encoding.UTF8.GetBytes("downloaded image"))
                };
                response.Content.Headers.ContentType = new(zip ? "application/zip" : "image/jpeg");
                return Task.FromResult(response);
            }
        }
    }
}
