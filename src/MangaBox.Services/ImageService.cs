using CardboardBox.Json;
using SixLabors.ImageSharp;
using System.Net.Http.Headers;

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
	/// <returns>The resulting image or null if not found</returns>
	Task<ImageResult> Get(Guid id);

	/// <summary>
	/// Gets the image data
	/// </summary>
	/// <param name="image">The image to fetch</param>
	/// <returns>The resulting image or null if not found</returns>
	Task<ImageResult> Get(MangaBoxType<MbImage> image);
}

internal class ImageService(
	IDbService _db,
	IApiService _api,
	IConfiguration _config,
	IJsonService _json,
	ILogger<ImageService> _logger) : IImageService
{
	public const string DIR_COVERS = "covers";
	public const string DIR_PAGES = "pages";
	public const string EXT_DAT = ".dat";

	public string StoragePath => field ??= _config["CacheDirectory"] ?? "file-cache";

	public async Task<ImageResult> Get(Guid id)
	{
		var image = await _db.Image.FetchWithRelationships(id);
		if (image is null) return new("Could not find image with that ID", id);

		return await Get(image);
	}

	public Task<HttpResponseMessage?> GetResponse(MbImage image, MbManga manga, MbSource source)
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
		});

		return request.Result();
	}

	public static string? FileName(HttpContentHeaders headers, string url)
	{
		var path = headers.ContentDisposition?.FileName
			?? headers.ContentDisposition?.Parameters?.FirstOrDefault()?.Value;
		if (!string.IsNullOrEmpty(path)) return path;

		return url.Split('/', StringSplitOptions.RemoveEmptyEntries).Last().Split('?').First();
	}

	public async Task<ImageResult> Get(MangaBoxType<MbImage> entity)
	{
		var image = entity.Entity;
		if (image.DeletedAt is not null) return new("Image not found (1)", entity.Entity.Id);

		var path = GetCachePath(image, out var hash);
		if (File.Exists(path) &&
			!string.IsNullOrEmpty(image.FileName) &&
			!string.IsNullOrEmpty(image.MimeType))
			return new(null, entity.Entity.Id, File.OpenRead(path),
				image.FileName, image.MimeType, image.ImageWidth, image.ImageHeight);

		var source = entity.GetItem<MbSource>();
		if (source is null) return new("Could not find source provider for image", entity.Entity.Id);
		var manga = entity.GetItem<MbManga>();
		if (manga is null) return new("Could not find manga for image", entity.Entity.Id);

		using var response = await GetResponse(image, manga, source);
		if (response is null) return new("Image not found (2)", entity.Entity.Id);
		if (!response.IsSuccessStatusCode)
		{
			var content = await response.Content.ReadAsStringAsync();
			_logger.LogWarning("Failed to fetch image >> {URL} >> {Status}: {Content}",
				image.Url, response.StatusCode, content);
			return new("Image not found (3)", entity.Entity.Id);
		}

		image.MimeType ??= response.Content.Headers.ContentType?.MediaType
			?? response.Content.Headers.ContentType?.ToString().Split(';').First()
			?? "application/octet-stream";
		image.ImageSize ??= response.Content.Headers.ContentLength;
		image.FileName ??= FileName(response.Content.Headers, image.Url);
		image.UrlHash = hash;

		using var io = File.Create(path);
		await response.Content.CopyToAsync(io);
		await io.FlushAsync();
		await io.DisposeAsync();

		if (image.ImageWidth is null || image.ImageHeight is null)
		{
			var (width, height) = await DetermineImageSize(path);
			image.ImageWidth = width ?? image.ImageWidth;
			image.ImageHeight = height ?? image.ImageHeight;
		}

		await _db.Image.Update(image);

		return new(null, entity.Entity.Id, File.OpenRead(path),
			image.FileName, image.MimeType, image.ImageWidth, image.ImageHeight);
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
		return Path.Combine(path, hash + EXT_DAT);
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
}

/// <summary>
/// Represents a resulting image from the image service
/// </summary>
/// <param name="Error">The error message if applicable</param>
/// <param name="Stream">The image stream</param>
/// <param name="FileName">The name of the file</param>
/// <param name="MimeType">The mime-type / content-type</param>
/// <param name="FileId">The ID of the file</param>
/// <param name="Width">The width of the image in pixels</param>
/// <param name="Height">The height of the image in pixels</param>
public record class ImageResult(
	string? Error,
	Guid FileId,
	Stream? Stream = null,
	string? FileName = null,
	string? MimeType = null,
	int? Width = null,
	int? Height = null);