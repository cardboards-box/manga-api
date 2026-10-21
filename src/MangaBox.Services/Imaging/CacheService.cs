using CardboardBox.Json;

namespace MangaBox.Services.Imaging;

/// <summary>
/// A service for caching files
/// </summary>
public interface ICacheService
{
	/// <summary>
	/// The primary cache root. All new files are written here.
	/// </summary>
	string StoragePath { get; set; }

	/// <summary>Gets normalized cache roots in read order: primary, then additional roots without duplicates.</summary>
	IReadOnlyList<string> StoragePaths { get; }

	/// <summary>
	/// Enumerates raw and compressed image candidates within each root, in root
	/// priority order. Does not create directories or persist physical paths.
	/// </summary>
	/// <param name="image">The image whose URL and manga identify the relative cache key.</param>
	IEnumerable<string> GetImageCachePaths(MbImage image);

	/// <summary>Enumerates external image candidates in root priority order without creating directories.</summary>
	/// <param name="url">The external image URL.</param>
	IEnumerable<string> GetExternalCachePaths(string url);

	/// <summary>Enumerates ZIP candidates in root priority order without creating directories.</summary>
	/// <param name="url">The ZIP URL.</param>
	IEnumerable<string> GetZipCachePaths(string url);

	/// <summary>
	/// Gets the primary write path for the given image, creating its parent directory.
	/// </summary>
	/// <param name="image">The image to get the cache path for</param>
	/// <param name="hash">The hash of the image URL</param>
	/// <param name="compress">Whether or not to use compressed extensions</param>
	/// <returns>The path to the cache file</returns>
	string GetCachePath(MbImage image, out string hash, bool compress);

	/// <summary>
	/// Gets the primary write path for an external image, creating its parent directory.
	/// </summary>
	/// <param name="url">The image URL</param>
	/// <param name="hash">The hash of the image URL</param>
	/// <returns>The path to the cache file</returns>
	string GetCachePath(string url, out string hash);

	/// <summary>
	/// Gets the primary write path for a ZIP file, creating its parent directory.
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
	/// <returns>The metadata of the cached file</returns>
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
	public const string EXT_ZIP = "zip";
	public const string EXT_COMP = "gz";

	/// <inheritdoc />
	public string StoragePath
	{
		get => field ??= _config["Imaging:CacheDir"]?.ForceNull() ?? "file-cache";
		set => field = value;
	}

	/// <inheritdoc />
	public IReadOnlyList<string> StoragePaths => new[] { StoragePath }
		.Concat(_config.GetSection("Imaging:AdditionalCacheDirs").Get<string[]>() ?? [])
		.Where(root => !string.IsNullOrWhiteSpace(root))
		.Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)))
		.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
		.ToArray();

	/// <summary>Builds a stable, root-independent URL hash and relative path.</summary>
	private static string RelativePath(string url, string extension, params string[] parts) =>
		Path.Combine([.. parts, $"{url.MD5Hash()}.{extension}"]);

	/// <inheritdoc />
	public IEnumerable<string> GetImageCachePaths(MbImage image)
	{
		string[] parts = [image.MangaId.ToString(), image.ChapterId.HasValue ? DIR_PAGES : DIR_COVERS];
		foreach (var root in StoragePaths)
		{
			yield return Path.Combine(root, RelativePath(image.Url, EXT_DAT, parts));
			yield return Path.Combine(root, RelativePath(image.Url, EXT_COMP, parts));
		}
	}

	/// <inheritdoc />
	public IEnumerable<string> GetExternalCachePaths(string url) =>
		StoragePaths.Select(root => Path.Combine(root, RelativePath(url, EXT_DAT, DIR_EXTERNAL)));

	/// <inheritdoc />
	public IEnumerable<string> GetZipCachePaths(string url) =>
		StoragePaths.Select(root => Path.Combine(root, RelativePath(url, EXT_ZIP, DIR_ZIP)));

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
		var path = Path.Combine([Path.GetFullPath(StoragePath), .. parts]);
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
			_logger.LogInformation("Created image cache directory >> {Path}", path);
		}

		hash = url.MD5Hash();
		return Path.Combine(path, $"{hash}.{ext}");
	}

	/// <inheritdoc />
	public string GetCachePath(MbImage image, out string hash, bool compress)
	{
		return CachePath(image.Url, compress ? EXT_COMP : EXT_DAT, out hash,
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
		return CachePath(url, EXT_ZIP, out hash, DIR_ZIP);
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
