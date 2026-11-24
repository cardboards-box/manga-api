using MangaBox.Database;
using MangaBox.Models;
using MangaBox.Providers.Sources;
using MangaBox.Services.Imaging;
using SkiaSharp;

namespace MangaBox.Cli.Verbs.Tests;

/// <summary>Checks stored Comix pages through the production provider and rotating proxy pool.</summary>
internal static class ComixChapterImageHarness
{
	/// <summary>Downloads and decodes every stored page without modifying the database or image cache.</summary>
	/// <param name="db">Read access to the stored chapter and its image metadata.</param>
	/// <param name="source">The Comix provider whose retry behavior is checked.</param>
	/// <param name="http">The service that builds the stored image request headers.</param>
	/// <param name="proxies">The configured rotating proxy pool.</param>
	/// <param name="logger">The diagnostic logger.</param>
	/// <param name="chapterId">The stored Comix chapter ID.</param>
	/// <param name="token">The cancellation token for downloads and retry delays.</param>
	/// <returns>A task that completes when every page decodes, or fails on the first unavailable page.</returns>
	public static async Task Run(
		IDbService db,
		IComixSource source,
		IHttpService http,
		IProxiedHttpService proxies,
		ILogger logger,
		Guid chapterId,
		CancellationToken token)
	{
		var stored = await db.Chapter.FetchWithRelationships(chapterId)
			?? throw new InvalidOperationException($"Chapter {chapterId} was not found.");
		var info = stored.GetItem<MbSource>();
		if (!string.Equals(info?.Slug, source.Provider, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException($"Chapter {chapterId} is not a Comix chapter.");

		var manga = stored.GetItem<MbManga>();
		var images = stored.GetItems<MbImage>().OrderBy(image => image.Ordinal).ToArray();
		if (images.Length == 0)
			throw new InvalidOperationException($"Chapter {chapterId} has no stored pages.");

		logger.LogInformation("Checking {Count} Comix pages for chapter {ChapterId}: {Url}",
			images.Length, chapterId, stored.Entity.Url);
		foreach (var image in images)
		{
			token.ThrowIfCancellationRequested();
			var headers = http.HeadersFrom(image.Url, info, manga, image);
			headers[IProxiedHttpService.AFFINITY_HEADER] = stored.Entity.MangaId.ToString("N");
			if (!string.IsNullOrEmpty(stored.Entity.Url))
				headers[IDownloadService.CHAPTER_URL_HEADER] = stored.Entity.Url;

			using var result = await source.DownloadImage(proxies, image.Url, headers, token);
			if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
				throw new InvalidDataException($"Comix page {image.Ordinal} failed: HTTP {result.Response?.StatusCode}.");

			using var bitmap = SKBitmap.Decode(result.Stream);
			if (bitmap is null)
				throw new InvalidDataException($"Comix page {image.Ordinal} did not decode as an image.");

			logger.LogInformation("Comix page {Page} decoded: {Width}x{Height}, previous failures={Failures}",
				image.Ordinal, bitmap.Width, bitmap.Height, image.FailedCount);
		}
		logger.LogInformation("All {Count} Comix pages downloaded and decoded; database and image cache were unchanged",
			images.Length);
	}
}
