using CardboardBox.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;
using System.Net.Http.Headers;
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
	IApiService _api,
	IJsonService _json,
	IConfiguration _config,
	ISourceService _sources,
	IMangaLoaderService _loader,
	ILogger<ImageService> _logger) : IImageService
{
	public const string DIR_COVERS = "covers";
	public const string DIR_PAGES = "pages";
	public const string EXT_DAT = "dat";

	private int? _maxRequests;
	private double? _timeoutSec;

	/// <summary>
	/// The place where the image cache is stored
	/// </summary>
	public string StoragePath => field ??= _config["Imaging:CacheDir"]?.ForceNull() ?? "file-cache";

	/// <summary>
	/// The max image requests to do in a row
	/// </summary>
	public int MaxRequests => _maxRequests ??= int.TryParse(_config["Imaging:MaxRequests"], out var mr) ? mr : 25;

	/// <summary>
	/// The max timeout between sets of images in seconds
	/// </summary>
	public double TimeoutSeconds => _timeoutSec ??= double.TryParse(_config["Imaging:TimeoutSeconds"], out var ts) ? ts : 2.5;

	/// <summary>
	/// The max timeout between sets of images
	/// </summary>
	public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

	/// <summary>
	/// Gets the response of the image download request
	/// </summary>
	/// <param name="image">The image to download</param>
	/// <param name="manga">The manga the image is attached to</param>
	/// <param name="source">The source of the manga</param>
	/// <param name="token">The cancellation token for the reqest</param>
	/// <returns>The response of the image download request</returns>
	public Task<HttpResponseMessage?> GetResponse(MbImage image, MbManga manga, MbSource source, CancellationToken token)
	{
		var ua = manga.UserAgent ?? source.UserAgent;
		var referer = manga.Referer ?? source.Referer;
		var headers = source.Headers ?? [];

		var request = _api.Create(image.Url, _json, "GET");
		request.Message(c =>
		{
			if (!string.IsNullOrEmpty(ua))
				c.Headers.UserAgent.ParseAdd(ua);
			if (!string.IsNullOrEmpty(referer))
				c.Headers.Referrer = new Uri(referer);

			foreach (var header in headers)
				c.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}).CancelWith(token);

		return request.Result();
	}

	/// <summary>
	/// Gets the file name from the content disposition
	/// </summary>
	/// <param name="headers">The headers of the response</param>
	/// <param name="url">The URL of the image</param>
	/// <param name="mimeType">The MIME type of the image</param>
	/// <returns>The file name of the image</returns>
	public static string? FileName(HttpContentHeaders headers, string url, string? mimeType)
	{
		var path = headers.ContentDisposition?.FileName
			?? headers.ContentDisposition?.Parameters?.FirstOrDefault()?.Value;
		if (!string.IsNullOrEmpty(path)) return path;

		path = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Split('?').FirstOrDefault();
		if (!string.IsNullOrEmpty(path)) return path;

		var ext = DetermineExtension(mimeType);
		return $"image.{ext}";
	}

	/// <summary>
	/// Ensures the file name is unique within the set
	/// </summary>
	/// <param name="fileName">The file name of the image</param>
	/// <param name="files">The set of existing file names to ensure uniqueness</param>
	/// <returns>The unique file name</returns>
	public static string FixFileName(string fileName, HashSet<string> files)
	{
		fileName = fileName.PurgePathChars();
		if (!string.IsNullOrEmpty(fileName) && files.Add(fileName)) return fileName;

		var name = Path.GetFileNameWithoutExtension(fileName)?.ForceNull() ?? "image";
		var ext = Path.GetExtension(fileName)?.Trim('.')?.ForceNull() ?? EXT_DAT;
		int index = 1;
		while(!files.Add(fileName))
		{
			fileName = $"{name}_{index}.{ext}";
			index++;
		}

		return fileName;
	}

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
		try
		{
			var path = GetCachePath(image, out var hash);
			if (File.Exists(path) &&
				!string.IsNullOrEmpty(image.FileName) &&
				!string.IsNullOrEmpty(image.MimeType))
				return new(null, image, File.OpenRead(path), true, overrideOrdinal);

			var loader = await _sources.FindById(source.Id, token);
			if (loader is null)
				return new("Could not find source provider for image", image, OverrideOrdinal: overrideOrdinal);

			using var lease = await loader.RateLimits.AcquireAsync(1, token);
			if (!lease.IsAcquired)
			{
				_logger.LogWarning("Rate limit timeout reached when fetching image >> {URL}", image.Url);
				var after = lease.TryGetMetadata(MetadataName.RetryAfter, out var val) ? val : TimeSpan.FromSeconds(5);
				return new("Rate Limits Reached - Retry after " + after, image, OverrideOrdinal: overrideOrdinal);
			}

			using var response = await GetResponse(image, manga, source, token);
			if (response is null) return new("Image not found (2)", image, OverrideOrdinal: overrideOrdinal);
			if (!response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync(token);
				_logger.LogWarning("Failed to fetch image >> {URL} >> {Status}: {Content}",
					image.Url, response.StatusCode, content);
				return new("Image not found (3)", image, OverrideOrdinal: overrideOrdinal);
			}

			image.MimeType ??= response.Content.Headers.ContentType?.MediaType
				?? response.Content.Headers.ContentType?.ToString().Split(';').First()
				?? "application/octet-stream";
			image.ImageSize ??= response.Content.Headers.ContentLength;
			image.FileName ??= FileName(response.Content.Headers, image.Url, image.MimeType);
			image.UrlHash = hash;

			using var io = File.Create(path);
			await response.Content.CopyToAsync(io, token);
			await io.FlushAsync(token);
			await io.DisposeAsync();

			if (image.ImageWidth is null || image.ImageHeight is null)
			{
				var (width, height) = await DetermineImageSize(path);
				image.ImageWidth = width ?? image.ImageWidth;
				image.ImageHeight = height ?? image.ImageHeight;
			}

			await _db.Image.Update(image);

			return new(null, image, File.OpenRead(path), false, overrideOrdinal);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to fetch image >> {URL}", image.Url);
			return new("Image not found (4) - " + ex.Message, image, OverrideOrdinal: overrideOrdinal);
		}
	}

	/// <summary>
	/// Gets the path to the cache for the given image
	/// </summary>
	/// <param name="image">The image to get the cache path for</param>
	/// <param name="hash">The hash of the image URL</param>
	/// <returns>The path to the cache file</returns>
	public string GetCachePath(MbImage image, out string hash)
	{
		var path = Path.Combine([..
			StoragePath.Split(['\\', '/']),
			image.MangaId.ToString(),
			image.ChapterId.HasValue ? DIR_PAGES : DIR_COVERS]);
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
			_logger.LogInformation("Created image cache directory >> {Path}", path);
		}

		hash = image.Url.MD5Hash();
		return Path.Combine(path, $"{hash}.{EXT_DAT}");
	}

	/// <summary>
	/// Determine the size of the images in pixels
	/// </summary>
	/// <param name="path">The path to the image file</param>
	/// <returns>A tuple containing the width and height of the image, or null if it cannot be determined</returns>
	public async Task<(int? width, int? height)> DetermineImageSize(string path)
	{
		try
		{
			using var image = await Image.LoadAsync(path);
			return (image.Width, image.Height);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to determine image size for >> {Path}", path);
			return (null, null);
		}
	}
	
	/// <summary>
	/// Determines the file extension for the given MIME type
	/// </summary>
	/// <param name="mimeType">The MIME type to determine the extension for</param>
	/// <returns>The file extension associated with the MIME type, or a default if unknown</returns>
	public static string DetermineExtension(string? mimeType)
	{
		if (string.IsNullOrEmpty(mimeType))
			return EXT_DAT;

		return MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault() ?? EXT_DAT;
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
				?? DetermineExtension(image.MimeType);
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