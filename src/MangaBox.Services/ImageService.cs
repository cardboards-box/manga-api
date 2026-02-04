using CardboardBox.Json;
using System.IO.Compression;
using System.Net.Http.Headers;

using Image = SixLabors.ImageSharp.Image;

namespace MangaBox.Services;

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
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The zip download result</returns>
	Task<ZipResult> Download(Guid chapterId, CancellationToken token);

	/// <summary>
	/// Download the chapter as a zip
	/// </summary>
	/// <param name="chapter">The chapter to download</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The zip download result</returns>
	Task<ZipResult> Download(MangaBoxType<MbChapter> chapter, CancellationToken token);
}

internal class ImageService(
	IDbService _db,
	IApiService _api,
	IConfiguration _config,
	IJsonService _json,
	IMangaLoaderService _loader,
	ILogger<ImageService> _logger) : IImageService
{
	public const string DIR_COVERS = "covers";
	public const string DIR_PAGES = "pages";
	public const string EXT_DAT = "dat";

	private int? _maxRequests;
	private double? _timeoutSec;

	public string StoragePath => field ??= _config["Imaging:CacheDir"]?.ForceNull() ?? "file-cache";

	public int MaxRequests => _maxRequests ??= int.TryParse(_config["Imaging:MaxRequests"], out var mr) ? mr : 25;

	public double TimeoutSeconds => _timeoutSec ??= double.TryParse(_config["Imaging:TimeoutSeconds"], out var ts) ? ts : 2.5;

	public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

	public async Task<ImageResult> Get(Guid id, CancellationToken token)
	{
		var image = await _db.Image.FetchWithRelationships(id);
		if (image is null) return new("Could not find image with that ID", new() { Id = id });

		return await Get(image, token);
	}

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

	public async Task<ImageResult> Get(MangaBoxType<MbImage> entity, CancellationToken token)
	{
		var image = entity.Entity;
		if (image.DeletedAt is not null) return new("Image not found (1)", image);

		var source = entity.GetItem<MbSource>();
		if (source is null) return new("Could not find source provider for image", image);
		var manga = entity.GetItem<MbManga>();
		if (manga is null) return new("Could not find manga for image", image);

		return await Get(source, manga, image, token);
	}

	public async Task<ImageResult> Get(MbSource source, MbManga manga, MbImage image, CancellationToken token)
	{
		var path = GetCachePath(image, out var hash);
		if (File.Exists(path) &&
			!string.IsNullOrEmpty(image.FileName) &&
			!string.IsNullOrEmpty(image.MimeType))
			return new(null, image, File.OpenRead(path), true);

		using var response = await GetResponse(image, manga, source, token);
		if (response is null) return new("Image not found (2)", image);
		if (!response.IsSuccessStatusCode)
		{
			var content = await response.Content.ReadAsStringAsync(token);
			_logger.LogWarning("Failed to fetch image >> {URL} >> {Status}: {Content}",
				image.Url, response.StatusCode, content);
			return new("Image not found (3)", image);
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

		return new(null, image, File.OpenRead(path), false);
	}

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

	public async Task<ZipResult> Download(Guid chapterId, CancellationToken token)
	{
		var result = await _loader.Pages(chapterId, false, token);
		if (result is null || result is not Boxed<MangaBoxType<MbChapter>> chapData) 
			return new("Could not find chapter with that ID");

		if (!chapData.Success || chapData.Data is null) 
			return new(chapData.Description ?? "Chapter came back empty");

		return await Download(chapData.Data, token);
	}

	public static string DetermineExtension(string? mimeType)
	{
		if (string.IsNullOrEmpty(mimeType))
			return EXT_DAT;

		return MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault() ?? EXT_DAT;
	}

	public async IAsyncEnumerable<ImageResult> Stream(MangaBoxType<MbChapter> data, 
		[EnumeratorCancellation] CancellationToken token, StringBuilder? issues = null)
	{
		issues ??= new();
		var chapter = data?.Entity;
		if (data is null || chapter is null) yield break;

		var source = data.GetItem<MbSource>();
		if (source is null) yield break;
		var manga = data.GetItem<MbManga>();
		if (manga is null) yield break;

		var images = data.GetItems<MbImage>().ToArray();
		if (images.Length == 0) yield break;

		int requests = 0;
		for(int i = 0; i < images.Length; i++)
		{
			token.ThrowIfCancellationRequested();

			if (requests >= MaxRequests)
			{
				_logger.LogInformation("[Chapter Download: {Id}] Max image requests reached, delaying for {Timeout} seconds", chapter.Id, TimeoutSeconds);
				await Task.Delay(Timeout, token);
				requests = 0;
				_logger.LogInformation("[Chapter Download: {Id}] Resuming image downloads", chapter.Id);
			}

			var result = await Get(source, manga, images[i], token);
			if (!string.IsNullOrEmpty(result.Error) || result.Stream is null)
			{
				var error = result.Error ?? "Image stream came back empty";
				issues.AppendLine($"- Failed to fetch image {images[i].Id} ({i + 1}/{images.Length}): {error}");
				_logger.LogWarning("[Chapter Download: {Id}] Skipping image {Index}/{Total} ({ImageId}) - {Error}",
					chapter.Id, i + 1, images.Length, images[i].Id, error);
				continue;
			}

			yield return result;

			if (!result.FromCache) requests++;
		}
	}

	public async Task<ZipResult> Download(MangaBoxType<MbChapter> data, CancellationToken token)
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
			var single = await Get(source, manga, images.First(), token);
			if (!string.IsNullOrEmpty(single.Error) || single.Stream is null)
				return new(single.Error ?? "Image not found");

			return new(null, single.Stream, single.FileName, single.MimeType);
		}

		var issues = new StringBuilder();
		var ms = new MemoryStream();
		using var zip = new ZipArchive(ms, ZipArchiveMode.Create, true);
		var found = new HashSet<string>();
		await foreach(var image in Stream(data, token, issues))
		{
			if (image.Stream is null) continue;

			var fileName = image.FileName ?? $"image.{DetermineExtension(image.MimeType)}";
			var entry = zip.CreateEntry(FixFileName(fileName, found));
			using var eio = entry.Open();
			await image.Stream.CopyToAsync(eio, token);
			await eio.FlushAsync(token);
			await image.Stream.DisposeAsync();
		}

		await zip.DisposeAsync();
		ms.Position = 0;

		return new(null, ms, 
			$"{manga.Id}_{chapter.Title?.ForceNull()?.PurgePathChars() ?? chapter.Ordinal.ToString()}.zip", 
			"application/zip");
	}
}

/// <summary>
/// Represents a file result
/// </summary>
/// <param name="Error">The error that occurred (if any)</param>
/// <param name="Stream">The stream of the zip file</param>
/// <param name="FileName">The name of the zip file</param>
/// <param name="MimeType">The mime-type / content-type</param>
public record class ZipResult(
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
public record class ImageResult(
	string? Error,
	MbImage Image,
	Stream? Stream = null,
	bool FromCache = true)
{
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