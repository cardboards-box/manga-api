using CardboardBox.Json;

namespace MangaBox.Services;

/// <summary>
/// A service for caching files
/// </summary>
public interface ICacheService
{
	/// <summary>
	/// The place where the image cache is stored
	/// </summary>
	string StoragePath { get; }

	/// <summary>
	/// Gets the path to the cache for the given image
	/// </summary>
	/// <param name="image">The image to get the cache path for</param>
	/// <param name="hash">The hash of the image URL</param>
	/// <returns>The path to the cache file</returns>
	string GetCachePath(MbImage image, out string hash);

	/// <summary>
	/// Gets the path to the cache for an external image
	/// </summary>
	/// <param name="url">The image URL</param>
	/// <param name="hash">The hash of the image URL</param>
	/// <returns>The path to the cache file</returns>
	string GetCachePath(string url, out string hash);

	/// <summary>
	/// Gets the path to the cache for a zip file
	/// </summary>
	/// <param name="url">The URL of the zip file</param>
	/// <param name="hash">The hash of the zip file URL</param>
	/// <returns>The path to the cache file</returns>
	string GetZipCachePath(string url, out string hash);

	/// <summary>
	/// Fetches the metadata from the internal cache
	/// </summary>
	/// <param name="cache">The cache path</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The metadata of the cached file</returns>x
	internal Task<ExternalMeta?> FetchMeta(string cache, CancellationToken token);

	/// <summary>
	/// Saves the metadata to the internal cache
	/// </summary>
	/// <param name="meta">The metadata</param>
	/// <param name="cache">The cache path</param>
	/// <param name="token">The cancellation token</param>
	internal Task SaveMeta(ExternalMeta meta, string cache, CancellationToken token);
}

internal class CacheService(
	IJsonService _json,
	IConfiguration _config,
	ILogger<CacheService> _logger) : ICacheService
{
	public const string DIR_COVERS = "covers";
	public const string DIR_PAGES = "pages";
	public const string DIR_EXTERNAL = "external";
	public const string DIR_ZIP = "zips";
	public const string EXT_DAT = HttpService.EXT_DAT;

	/// <inheritdoc />
	public string StoragePath => field ??= _config["Imaging:CacheDir"]?.ForceNull() ?? "file-cache";

	/// <summary>
	/// Generates a cache path
	/// </summary>
	/// <param name="url">The URL being cached</param>
	/// <param name="ext">The file extension</param>
	/// <param name="hash">The hash of the URL</param>
	/// <param name="parts">Additional path parts</param>
	/// <returns>The full path to the cache file</returns>
	public string CachePath(string url, string ext, out string hash, params string[] parts)
	{
		var path = Path.Combine([.. StoragePath.Split(['\\', '/']), .. parts]);
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
			_logger.LogInformation("Created image cache directory >> {Path}", path);
		}

		hash = url.MD5Hash();
		return Path.Combine(path, $"{hash}.{ext}");
	}

	/// <inheritdoc />
	public string GetCachePath(MbImage image, out string hash)
	{
		return CachePath(image.Url, EXT_DAT, out hash,
			image.MangaId.ToString(),
			image.ChapterId.HasValue ? DIR_PAGES : DIR_COVERS);
	}

	/// <inheritdoc />
	public string GetCachePath(string url, out string hash)
	{
		return CachePath(url, EXT_DAT, out hash, DIR_EXTERNAL);
	}

	/// <inheritdoc />
	public string GetZipCachePath(string url, out string hash)
	{
		return CachePath(url, "zip", out hash, DIR_ZIP);
	}

	/// <inheritdoc />
	public async Task<ExternalMeta?> FetchMeta(string cache, CancellationToken token)
	{
		try
		{
			var path = Path.ChangeExtension(cache, "json");
			if (!File.Exists(path)) return null;

			using var io = File.OpenRead(path);
			return await _json.Deserialize<ExternalMeta>(io, token);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load metadata for path: {Path}", cache);
			return null;
		}
	}

	/// <inheritdoc />
	public async Task SaveMeta(ExternalMeta meta, string cache, CancellationToken token)
	{
		try
		{
			var path = Path.ChangeExtension(cache, "json");
			using var io = File.Create(path);
			await _json.Serialize(meta, io, token);
			await io.FlushAsync(token);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save metadata for path: {Path}", cache);
		}
	}

}

internal record class ExternalMeta(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("mimeType")] string? MimeType,
	[property: JsonPropertyName("createdAt")] DateTime CreatedAt);
