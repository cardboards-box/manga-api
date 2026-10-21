using System.Diagnostics;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using MangaBox.Database.Services;
using MangaBox.Models;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Read-only timings for source refresh, image downloads, and image replacement queries.</summary>
internal static class MangaKatanaHarness
{
    /// <summary>
    /// Measures the provider and plain HTTP on one stored (or supplied) chapter,
    /// decodes one reader image, and explains slice-reference lookup plans.
    /// EXPLAIN is deliberately used without ANALYZE or any data modification.
    /// </summary>
    public static async Task Run(IMangaKatanaSource source, IApiService api, ISqlService sql,
        IHttpService http, ILogger logger, string? url, CancellationToken token)
    {
        await CheckBust(sql, logger, token);
        var fixture = new HtmlAgilityPack.HtmlDocument();
        fixture.LoadHtml("<script>var thzq = [ 'https://example.test/1.jpg',\n\"https://example.test/2.jpg?token=abc\" ];</script>");
        if (!MangaKatanaSource.ParsePages(fixture).Select(p => p.Page).SequenceEqual([
            "https://example.test/1.jpg", "https://example.test/2.jpg?token=abc"]))
            throw new InvalidDataException("MangaKatana page array order or whitespace parsing changed.");
        url ??= (await sql.Get<string>("""
            SELECT c.url FROM mb_chapters c
            JOIN mb_manga m ON m.id = c.manga_id
            JOIN mb_sources s ON s.id = m.source_id
            WHERE s.slug = 'mangakatana' AND c.deleted_at IS NULL AND m.deleted_at IS NULL
            ORDER BY c.updated_at DESC LIMIT 1
            """)).FirstOrDefault() ?? throw new InvalidOperationException("No MangaKatana chapter found; supply --url.");
        var (matches, id) = source.MatchesProvider(url);
        if (!matches || id is null) throw new InvalidOperationException("Not a MangaKatana chapter URL.");
        var chapterId = new Uri(url).AbsolutePath.TrimEnd('/').Split('/').Last();
        var watch = Stopwatch.StartNew();
        using (var response = await api.Get(url, request => request.Timeout(15), token))
        {
            response?.EnsureSuccessStatusCode();
            var html = response is null ? "" : await response.Content.ReadAsStringAsync(token);
            logger.LogInformation("MangaKatana direct HTML: {Milliseconds:F0} ms, page array present: {HasPages}",
                watch.Elapsed.TotalMilliseconds, Regex.IsMatch(html, @"thzq\s*=\s*\["));
        }
        watch.Restart();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var pages = await source.ChapterPages(id, chapterId, timeout.Token);
        if (pages.Length == 0) throw new InvalidDataException("MangaKatana returned no pages.");
        logger.LogInformation("MangaKatana provider refresh: {Milliseconds:F0} ms, {Pages} pages", watch.Elapsed.TotalMilliseconds, pages.Length);
        watch.Restart();
        using var download = await source.DownloadImage(http, pages[0].Page,
            new() { ["Referer"] = source.HomeUrl, ["User-Agent"] = source.UserAgent! }, token);
        if (download.Error is not null || download.Stream is null) throw new InvalidDataException(download.Error ?? "Empty image.");
        using var bitmap = SkiaSharp.SKBitmap.Decode(download.Stream);
        if (bitmap is null) throw new InvalidDataException("MangaKatana returned invalid image data.");
        logger.LogInformation("MangaKatana image download/decode: {Milliseconds:F0} ms", watch.Elapsed.TotalMilliseconds);

        // Compare the old global slice search with the same-manga search. The
        // restitcher only accepts slices from the original chapter of that manga.
        foreach (var scoped in new[] { false, true })
        {
            var scope = scoped ? "manga_id = (SELECT manga_id FROM mb_images WHERE id = :id) AND cardinality(slices) > 0 AND " : "";
            var query = $"""
                EXPLAIN SELECT id FROM mb_images
                WHERE {scope}EXISTS (
                    SELECT 1 FROM unnest(mb_images.slices) AS u(image_id, slice_ordinal, slice_start, slice_stop)
                    WHERE u.image_id = :id
                )
                """;
            var sample = (await sql.Get<Guid>("SELECT id FROM mb_images WHERE deleted_at IS NULL LIMIT 1")).First();
            var plan = await sql.Get<string>(query, new { id = sample });
            logger.LogInformation("Cache-bust slice lookup ({Scope}):\n{Plan}", scoped ? "same manga" : "global", string.Join('\n', plan));
        }
    }

    /// <summary>
    /// Runs the production replacement SQL against temporary tables in a rolled-back
    /// transaction. Public tables are never written. Checks ID rotation, failed-state
    /// reset, list covers, slice ordering, and unrelated manga isolation.
    /// </summary>
    private static async Task CheckBust(ISqlService sql, ILogger logger, CancellationToken token)
    {
        using var connection = await sql.CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var original = Guid.NewGuid();
            var manga = Guid.NewGuid();
            var chapter = Guid.NewGuid();
            var slicer = Guid.NewGuid();
            var untouched = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition("""
                CREATE TEMP TABLE mb_images (LIKE public.mb_images INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES) ON COMMIT DROP;
                CREATE TEMP TABLE mb_list_ext (LIKE public.mb_list_ext INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES) ON COMMIT DROP;
                INSERT INTO mb_images (id, manga_id, chapter_id, ordinal, url, file_name, failed_count, failed_reason, indexed)
                VALUES (:original, :manga, :chapter, 1, 'https://example.test/1.jpg', 'old.jpg', 4, 'old failure', true);
                INSERT INTO mb_images (id, manga_id, chapter_id, ordinal, url, slices)
                VALUES (:slicer, :manga, :chapter, 2, 'https://example.test/slice.jpg', ARRAY[
                    ROW(:original, 1, 0, 100)::mb_image_slice,
                    ROW(:original, 2, 100, 200)::mb_image_slice
                ]);
                INSERT INTO mb_images (id, manga_id, chapter_id, ordinal, url)
                VALUES (:untouched, :otherManga, :chapter, 1, 'https://example.test/other.jpg');
                INSERT INTO mb_list_ext (list_id, cover_id, manga_count, cloned_count) VALUES (:list, :original, 1, 0);
                """, new { original, manga, chapter, slicer, untouched, otherManga = Guid.NewGuid(), list = Guid.NewGuid() }, transaction,
                cancellationToken: token));
            var replacement = await connection.QuerySingleAsync<MbImage>(new CommandDefinition(
                MbImageDbService.BUST_QUERY, new { id = original }, transaction, cancellationToken: token));
            if (replacement.Id == original || replacement.Ordinal != 1 || replacement.FileName is not null ||
                replacement.FailedCount != 0 || replacement.Indexed)
                throw new InvalidDataException("Image replacement failed to rotate the ID and reset failed state.");
            var valid = await connection.QuerySingleAsync<bool>(new CommandDefinition("""
                SELECT
                    (SELECT deleted_at IS NOT NULL FROM mb_images WHERE id = :original) AND
                    (SELECT cover_id = :replacement FROM mb_list_ext LIMIT 1) AND
                    (SELECT slices[1] = ROW(:replacement, 1, 0, 100)::mb_image_slice
                        AND slices[2] = ROW(:replacement, 2, 100, 200)::mb_image_slice
                        FROM mb_images WHERE id = :slicer) AND
                    (SELECT deleted_at IS NULL AND cardinality(slices) = 0 FROM mb_images WHERE id = :untouched)
                """, new { original, replacement = replacement.Id, slicer, untouched }, transaction, cancellationToken: token));
            if (!valid) throw new InvalidDataException("Image replacement damaged cover or slice references.");
            logger.LogInformation("Cache-bust SQL harness passed: replacement, covers and slices verified in temporary tables");
        }
        finally { transaction.Rollback(); }
    }
}
