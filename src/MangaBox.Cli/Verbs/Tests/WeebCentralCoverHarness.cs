using MangaBox.Database;
using MangaBox.Models;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Diagnoses and verifies stored covers, with an explicit operation to repair verified failure metadata.</summary>
internal static class WeebCentralCoverHarness
{
    /// <summary>
    /// Runs active covers through the website's image service, including source
    /// lookup, cooldowns and caching. Optional repair clears old failure state
    /// only after the returned bytes decode; image IDs and URLs remain stable.
    /// </summary>
    public static async Task Serve(ISqlService sql, IDbService db, IImageService service, ILogger logger, bool repair, CancellationToken token)
    {
        var ids = await sql.Get<Guid>("""
            SELECT i.id FROM mb_images i JOIN mb_manga m ON m.id = i.manga_id
            JOIN mb_sources s ON s.id = m.source_id
            WHERE s.slug = 'weebcentral' AND i.chapter_id IS NULL AND i.deleted_at IS NULL AND m.deleted_at IS NULL
            ORDER BY m.title
            """);
        if (ids.Length == 0) throw new InvalidOperationException("No active WeebCentral covers found.");
        foreach (var id in ids)
        {
            using var result = await service.Get(id, token);
            if (result.Error is not null || result.Stream is null)
                throw new InvalidOperationException($"Website cover path failed: {id}: {result.Error}");
            using var bitmap = SkiaSharp.SKBitmap.Decode(result.Stream);
            if (bitmap is null) throw new InvalidDataException($"Website cover path returned invalid image data: {id}");
            if (repair && (result.Image.FailedCount > 0 || result.Image.LastFailedAt is not null || result.Image.FailedReason is not null))
            {
                result.Image.FailedCount = 0;
                result.Image.LastFailedAt = null;
                result.Image.FailedReason = null;
                await db.Image.Update(result.Image);
                logger.LogInformation("Cleared stale failure state on verified cover {Id}", id);
            }
            logger.LogInformation("Website cover path passed for {Id}: {Width}x{Height}, cached={Cached}, previous failures={Failures}",
                id, bitmap.Width, bitmap.Height, result.FromCache, result.Image.FailedCount);
        }
    }

    /// <summary>Reads persisted cover status and downloads active cover records with their real request headers.</summary>
    public static async Task Inspect(IWeebCentralSource source, ISqlService sql, IDbService db,
        IHttpService http, IProxiedHttpService proxy, ILogger logger, CancellationToken token)
    {
        var covers = await sql.Get<Cover>("""
            SELECT m.title, m.id AS manga_id, i.id AS image_id, i.url, i.failed_count,
                left(i.failed_reason, 250) AS failed_reason, i.last_failed_at, i.deleted_at,
                i.mime_type, i.file_name, i.image_size, i.image_width, i.image_height
            FROM mb_manga m JOIN mb_sources s ON s.id = m.source_id
            LEFT JOIN mb_images i ON i.manga_id = m.id AND i.chapter_id IS NULL
            WHERE s.slug = 'weebcentral' AND m.deleted_at IS NULL
            ORDER BY m.title, i.created_at DESC
            """);
        if (covers.Length == 0) throw new InvalidOperationException("No stored WeebCentral manga found.");
        foreach (var cover in covers)
        {
            logger.LogInformation("Stored WeebCentral cover: {Cover}", JsonSerializer.Serialize(cover));
            if (cover.ImageId is not Guid id || cover.DeletedAt is not null) continue;
            var full = await db.Image.FetchWithRelationships(id)
                ?? throw new InvalidOperationException($"Cover record {id} could not be read.");
            var headers = http.HeadersFrom(full.Entity.Url, full.GetItem<MbSource>(), full.GetItem<MbManga>(), full.Entity);
            using var result = await source.DownloadImage(proxy, full.Entity.Url, headers, token);
            if (result.Error is not null || result.Stream is null)
                throw new InvalidOperationException($"Stored cover failed to download: {id}: {result.Error}");
            using var image = SkiaSharp.SKBitmap.Decode(result.Stream);
            if (image is null) throw new InvalidDataException($"Stored cover returned invalid image data: {id}");
            logger.LogInformation("Stored cover {Id} decoded: {Width}x{Height}, MIME {Mime}, filename {Filename}",
                id, image.Width, image.Height, result.MimeType, result.FileName);
        }
    }

    /// <summary>Read-only snapshot of a stored cover, including failure and cache metadata.</summary>
    private sealed class Cover
    {
        /// <summary>The manga title.</summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>The manga ID.</summary>
        public Guid MangaId { get; set; }
        /// <summary>The cover ID, absent if no cover was imported.</summary>
        public Guid? ImageId { get; set; }
        /// <summary>The upstream cover URL.</summary>
        public string? Url { get; set; }
        /// <summary>The stored failure count.</summary>
        public int? FailedCount { get; set; }
        /// <summary>A bounded excerpt of the failure history.</summary>
        public string? FailedReason { get; set; }
        /// <summary>The most recent stored failure time.</summary>
        public DateTime? LastFailedAt { get; set; }
        /// <summary>The cover's soft-deletion time, if any.</summary>
        public DateTime? DeletedAt { get; set; }
        /// <summary>The stored media type.</summary>
        public string? MimeType { get; set; }
        /// <summary>The stored filename.</summary>
        public string? FileName { get; set; }
        /// <summary>The downloaded byte count.</summary>
        public long? ImageSize { get; set; }
        /// <summary>The stored pixel width.</summary>
        public int? ImageWidth { get; set; }
        /// <summary>The stored pixel height.</summary>
        public int? ImageHeight { get; set; }
    }
}
