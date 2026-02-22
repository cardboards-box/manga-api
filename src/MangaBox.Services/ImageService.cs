using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Image = SixLabors.ImageSharp.Image;

namespace MangaBox.Services;

using CBZModels;

/// <summary>
/// A service for interacting with images
/// </summary>
public interface IImageService
{
	/// <summary>
	/// How long to wait between requesting failed images
	/// </summary>
	TimeSpan ErrorWaitPeriod { get; }

	/// <summary>
	/// Get the image by it's ID
	/// </summary>
	/// <param name="id">The ID of the image</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The resulting image or null if not found</returns>
	Task<ImageResult> Get(Guid id, CancellationToken token);

	/// <summary>
	/// Gets the image data
	/// </summary>
	/// <param name="image">The image to fetch</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The resulting image or null if not found</returns>
	Task<ImageResult> Get(MangaBoxType<MbImage> image, CancellationToken token);

	/// <summary>
	/// Download an external image by it's URL
	/// </summary>
	/// <param name="url">The URL of the image</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The download result</returns>
	Task<SingleFileResult> Download(string url, CancellationToken token);

	/// <summary>
	/// Download the chapter as a zip
	/// </summary>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="format">The format to download</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The zip download result</returns>
	Task<SingleFileResult> Download(Guid chapterId, ComicFormat format, CancellationToken token);

	/// <summary>
	/// Download the chapter as a zip
	/// </summary>
	/// <param name="chapter">The chapter to download</param>
	/// <param name="format">The format to download</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The zip download result</returns>
	Task<SingleFileResult> Download(MangaBoxType<MbChapter> chapter, ComicFormat format, CancellationToken token);

	/// <summary>
	/// Combines the given images into a single file
	/// </summary>
	/// <param name="ids">The IDs of the images to combine</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The image result</returns>
	Task<SingleFileResult> Combine(Guid[] ids, CancellationToken token);
}

/// <inheritdoc cref="IImageService" />
internal class ImageService(
	IDbService _db,
	IZipService _zip,
	IHttpService _http,
	ICacheService _cache,
	IConfiguration _config,
	ISourceService _sources,
	IMangaLoaderService _loader,
	ILogger<ImageService> _logger) : IImageService
{
	private int? _failures;
	private TimeSpan? _waitPeriod;

	/// <summary>
	/// The number of times to wait before the image gets deleted
	/// </summary>
	public int FailuresBeforeDelete => _failures ??= int.TryParse(_config["Imaging:FailuresBeforeDelete"], out var f) ? f : 4;

	/// <summary>
	/// How long to wait between requesting failed images in seconds
	/// </summary>
	public double ErrorWaitPeriodSeconds => double.TryParse(_config["Imaging:ErrorWaitPeriod"], out var sec) ? sec : 60 * 60 * 24;

	/// <summary>
	/// How long to wait between requesting failed images
	/// </summary>
	public TimeSpan ErrorWaitPeriod => _waitPeriod ??= TimeSpan.FromSeconds(ErrorWaitPeriodSeconds);

	/// <summary>
	/// Fetches the image result
	/// </summary>
	/// <param name="source">The source of the manga</param>
	/// <param name="manga">The manga the image belongs to</param>
	/// <param name="image">The image to download</param>
	/// <param name="overrideOrdinal">The optional override ordinal for the images</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the image</returns>
	public async Task<ImageResult> Get(MbSource source, MbManga manga, MbImage image, int? overrideOrdinal, CancellationToken token)
	{
		async Task<ImageResult> HandleError(string reason)
		{
			image.LastFailedAt = DateTime.UtcNow;

			if (!string.IsNullOrEmpty(image.FailedReason))
				image.FailedReason += "\n" + reason;
			else image.FailedReason = reason;

			image.FailedCount += 1;
			await _db.Image.Update(image);

			if (image.FailedCount > FailuresBeforeDelete)
				await _db.Image.Delete(image.Id);

			_logger.LogWarning("Image failed to load: {Id} ({Count}) >> {Url} >> {Reason}", image.Id, image.FailedCount, image.Url, reason);
			return new(reason, image, OverrideOrdinal: overrideOrdinal);
		}

		try
		{
			var path = _cache.GetCachePath(image, out var hash);
			if (File.Exists(path) &&
				!string.IsNullOrEmpty(image.FileName) &&
				!string.IsNullOrEmpty(image.MimeType))
				return new(null, image, File.OpenRead(path), true, overrideOrdinal);

			if (image.LastFailedAt is not null && image.LastFailedAt.Value + ErrorWaitPeriod > DateTime.UtcNow)
			{
				var waitTime = (image.LastFailedAt.Value + ErrorWaitPeriod) - DateTime.UtcNow;
				return new($"Image is in cooldown period. Retry after {waitTime.TotalSeconds:F0} seconds", image, OverrideOrdinal: overrideOrdinal);
			}

			var loader = await _sources.FindById(source.Id, token);
			if (loader is null)
				return await HandleError("Could not find source provider for image");

			var limiter = loader.Service.GetRateLimiter(image.Url);
			using var lease = await limiter.AcquireAsync(1, token);
			if (!lease.IsAcquired)
			{
				var after = lease.TryGetMetadata(MetadataName.RetryAfter, out var val) ? val : TimeSpan.FromSeconds(5);
				return await HandleError("Rate Limits Reached - Retry after " + after);
			}

			var zipResult = await _zip.Get(source, manga, image, path, hash, overrideOrdinal, token);
			if (zipResult is not null)
			{
				await _db.Image.Update(image);
				return zipResult;
			}

			var headers = _http.HeadersFrom(image.Url, source, manga, image);
			using var download = await _http.Download(image.Url, headers, token);
			if (!string.IsNullOrEmpty(download.Error) || download.Stream is null)
				return await HandleError(download.Error ?? "Stream came back empty!");

			image.MimeType ??= download.MimeType;
			image.ImageSize ??= download.Length ?? download.Stream.Length;
			image.FileName ??= download.FileName;
			image.UrlHash = hash;

			using var io = File.Create(path);
			await download.Stream.CopyToAsync(io, token);
			await io.FlushAsync(token);
			await io.DisposeAsync();

			if (image.ImageWidth is null || image.ImageHeight is null)
			{
				var (width, height) = await _http.DetermineImageSize(path);
				image.ImageWidth = width ?? image.ImageWidth;
				image.ImageHeight = height ?? image.ImageHeight;
			}

			await _db.Image.Update(image);

			return new(null, image, File.OpenRead(path), false, overrideOrdinal);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to fetch image >> {URL}", image.Url);
			return await HandleError("Image not found (4) - " + ex.Message);
		}
	}

	/// <summary>
	/// Stream the images in the set
	/// </summary>
	/// <param name="set">The set of images to download</param>
	/// <param name="token">The cancellation token to cancel the operation</param>
	/// <returns>The stream of images</returns>
	public IAsyncEnumerable<ImageResult> Stream(MangaImageSet set, CancellationToken token)
	{
		var indexes = Enumerable.Range(0, set.Images.Length);
		return indexes.ParallelForeach(async (i, ct) =>
		{
			var image = set.Images[i];

			var manga = set.Manga.FirstOrDefault(m => m.Id == image.MangaId);
			if (manga is null) return new(
				$"Could not find manga for image {image.Id} >> {image.Url}", 
				image, null, false, i + 1);

			var source = set.Sources.FirstOrDefault(s => s.Id == manga.SourceId);
			if (source is null) return new(
				$"Could not find source for image {image.Id} >> {image.Url}", 
				image, null, false, i + 1);

			var result = await Get(source, manga, image, i + 1, token);
			if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
				return new(
					$"Failed to fetch image {image.Id} ({i + 1}/{set.Images.Length}): " +
					(result.Error ?? "Image stream came back empty"),
					image, null, false, i + 1);

			return result;
		}, null, token).OrderBy(t => t.Ordinal);
	}

	/// <inheritdoc />
	public async Task<ImageResult> Get(Guid id, CancellationToken token)
	{
		var image = await _db.Image.FetchWithRelationships(id);
		if (image is null) return new("Could not find image with that ID", new() { Id = id });

		return await Get(image, token);
	}

	/// <inheritdoc />
	public async Task<ImageResult> Get(MangaBoxType<MbImage> entity, CancellationToken token)
	{
		var image = entity.Entity;
		if (image.DeletedAt is not null) return new("Image not found (1)", image);

		var source = entity.GetItem<MbSource>();
		if (source is null) return new("Could not find source provider for image", image);
		var manga = entity.GetItem<MbManga>();
		if (manga is null) return new("Could not find manga for image", image);

		return await Get(source, manga, image, null, token);
	}

	/// <inheritdoc />
	public async Task<SingleFileResult> Download(Guid chapterId, ComicFormat format, CancellationToken token)
	{
		var result = await _loader.Pages(chapterId, false, token);
		if (result is null || result is not Boxed<MangaBoxType<MbChapter>> chapData) 
			return new("Could not find chapter with that ID");

		if (!chapData.Success || chapData.Data is null) 
			return new(chapData.Description ?? "Chapter came back empty");

		return await Download(chapData.Data, format, token);
	}

	/// <inheritdoc />
	public async Task<SingleFileResult> Download(MangaBoxType<MbChapter> data, ComicFormat format, CancellationToken token)
	{
		var chapter = data?.Entity;
		if (data is null || chapter is null) return new("Chapter does not exist");

		var source = data.GetItem<MbSource>();
		if (source is null) return new("Could not find source provider for the chapter");
		var manga = data.GetItem<MbManga>();
		if (manga is null) return new("Could not find manga for chapter");

		var images = data.GetItems<MbImage>().ToArray();
		if (images.Length == 0)
			return new(string.IsNullOrEmpty(chapter.ExternalUrl)
				? "Chapter has no images."
				: "Chapter is an external chapter");

		if (images.Length == 1)
		{
			var single = await Get(source, manga, images.First(), null, token);
			if (!string.IsNullOrEmpty(single.Error) || single.Stream is null)
				return new(single.Error ?? "Image not found");

			return new(null, single.Stream, single.FileName, single.MimeType);
		}

		var set = new MangaImageSet([manga], [source], images);
		var issues = new StringBuilder();
		var ms = new MemoryStream();
		using var zip = new ZipArchive(ms, ZipArchiveMode.Create, true);
		var pages = new List<ComicInfoPage>();
		await foreach(var image in Stream(set, token))
		{
			if (!string.IsNullOrEmpty(image.Error))
				issues.AppendLine(image.Error);

			if (image.Stream is null) continue;

			var pageExt = Path.GetExtension(image.FileName ?? "").Trim('.').ForceNull() 
				?? _http.DetermineExtension(image.MimeType);
			var filename = $"{image.Ordinal:0000}.{pageExt}";
			var entry = zip.CreateEntry(filename);
			pages.Add(new ComicInfoPage
			{
				Image = image.Ordinal,
				ImageSize = image.Image.ImageSize,
				ImageWidth = image.Width,
				ImageHeight = image.Height,
				PageType = ComicPageType.Story
			});
			using var eio = entry.Open();
			await image.Stream.CopyToAsync(eio, token);
			await eio.FlushAsync(token);
			await image.Stream.DisposeAsync();
		}

		if (format == ComicFormat.Cbz)
		{
			var cbzMeta = new ComicInfo
			{
				Title = chapter.Title ?? manga.Title,
				Summary = manga.Description,
				Number = chapter.Ordinal,
				Manga = true,
				Pages = pages
			};
			var metaEntry = zip.CreateEntry("ComicInfo.xml");
			using var metaIo = metaEntry.Open();
			ComicInfoXmlHelpers.SerializeToStream(cbzMeta, metaIo, true);
			await metaIo.FlushAsync(token);
		}

		await zip.DisposeAsync();
		ms.Position = 0;

		var ext = format == ComicFormat.Cbz ? "cbz" : "zip";
		var mime = format == ComicFormat.Cbz ? "application/vnd.comicbook+zip" : "application/zip";

		return new(null, ms, 
			$"{manga.Id}_{chapter.Title?.ForceNull()?.PurgePathChars() ?? chapter.Ordinal.ToString()}.{ext}",
			mime);
	}

	/// <inheritdoc />
	public async Task<SingleFileResult> Combine(Guid[] ids, CancellationToken token)
	{
		var set = await _db.Image.FetchSet(ids);
		if (set.Images.Length == 0)
			return new("No images found for the given IDs");

		var results = await Stream(set, token).ToList(token);
		var errors = results
			.Where(r => !string.IsNullOrEmpty(r.Error))
			.Select(r => r.Error!)
			.ToList();
		var images = results
			.Where(t => t.Stream is not null)
			.OrderBy(t => ids.IndexOf(t.Image.Id))
			.Select(t =>
			{
				try
				{
					return Image.Load(t.Stream!);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to load image >> {FileId}", t.FileId);
					errors.Add($"Failed to load image >> {t.FileId}: {ex.Message}");
					return null!;
				}
			})
			.Where(t => t is not null)
			.ToArray();

		if (images.Length == 0)
			return new("No images could be fetched: " + string.Join("; ", errors));

		int width = images.Max(t => t.Width),
			height = images.Sum(t => t.Height);

		using var image = new Image<Rgba32>(width, height);

		int y = 0;
		foreach (var img in images)
		{
			int x = (width / 2) - (img.Width / 2);
			var p = new Point(x, y);
			image.Mutate(t => t.DrawImage(img, p, 1));
			y += img.Height;
		}

		var output = new MemoryStream();
		await image.SaveAsPngAsync(output, token);
		output.Position = 0;

		images.Each(t => t.Dispose());
		await results.Select(async t =>
		{
			try
			{
				if (t.Stream is not null)
					await t.Stream.DisposeAsync();
			}
			catch { }
		}).WhenAll();
		return new(string.Join("; ", errors), output, "strip.png", "image/png");
	}

	/// <inheritdoc />
	public async Task<SingleFileResult> Download(string url, CancellationToken token)
	{
		try
		{
			var cachePath = _cache.GetCachePath(url, out _);
			ExternalMeta? meta;
			if (File.Exists(cachePath) && 
				(meta = await _cache.FetchMeta(cachePath, token)) is not null)
			{
				var stream = File.OpenRead(cachePath);
				return new(null, stream, meta.Name, meta.MimeType);
			}

			using var download = await _http.Download(url, null, token);
			if (!string.IsNullOrEmpty(download.Error) || download.Stream is null)
				return new(download.Error ?? "Stream came back empty!");

			meta = new(download.FileName ?? "image.webp", download.MimeType, DateTime.UtcNow);

			using var io = File.Create(cachePath);
			await download.Stream.CopyToAsync(io, token);
			await io.FlushAsync(token);
			await io.DisposeAsync();

			await _cache.SaveMeta(meta, cachePath, token);
			return new(null, File.OpenRead(cachePath), download.FileName, download.MimeType);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while loading external image: {URL}", url);
			return new(ex.Message);
		}
	}
}

/// <summary>
/// Represents a file result
/// </summary>
/// <param name="Error">The error that occurred (if any)</param>
/// <param name="Stream">The stream of the zip file</param>
/// <param name="FileName">The name of the zip file</param>
/// <param name="MimeType">The mime-type / content-type</param>
public record class SingleFileResult(
	string? Error,
	Stream? Stream = null,
	string? FileName = null,
	string? MimeType = null);

/// <summary>
/// Represents a resulting image from the image service
/// </summary>
/// <param name="Error">The error message if applicable</param>
/// <param name="Stream">The image stream</param>
/// <param name="FromCache">Indicates if the image was retrieved from cache or the source</param>
/// <param name="Image">The image data</param>
/// <param name="OverrideOrdinal">The optional ordinal to override the one on the base image</param>
public record class ImageResult(
	string? Error,
	MbImage Image,
	Stream? Stream = null,
	bool FromCache = true,
	int? OverrideOrdinal = null)
{
	/// <summary>
	/// The ordinal of the image in the set
	/// </summary>
	public int Ordinal => OverrideOrdinal ?? Image.Ordinal;

	/// <summary>
	/// The ID of the file
	/// </summary>
	public Guid FileId => Image.Id;

	/// <summary>
	/// The name of the file
	/// </summary>
	public string? FileName => Image?.FileName;

	/// <summary>
	/// The mime-type / content-type
	/// </summary>
	public string? MimeType => Image?.MimeType;

	/// <summary>
	/// The width of the image in pixels
	/// </summary>
	public int? Width => Image?.ImageWidth;

	/// <summary>
	/// The height of the image in pixels
	/// </summary>
	public int? Height => Image?.ImageHeight;
}