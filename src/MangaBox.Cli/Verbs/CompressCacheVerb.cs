using System.IO.Compression;

namespace MangaBox.Cli.Verbs;

using Services.Imaging;

[Verb("compress-cache", HelpText = "Compress the cache directory to save space")]
internal class CompressCache
{
	[Option('d', "directory", HelpText = "The directory to iterate through to compress the images")]
	public string? Directory { get; set; }

	[Option('m', "mode", HelpText = "The mode - compress (default) or decompress")]
	public string? Mode { get; set; }
}

internal class CompressCacheVerb(
	ICacheService _cache,
	ILogger<CompressCacheVerb> logger) : BooleanVerb<CompressCache>(logger)
{
	public const string EXT_COMP = "gz";
	public const string EXT_DAT = "dat";

	public static async Task Compress(string from, string to, CancellationToken token)
	{
		await using var input = File.OpenRead(from);
		await using var output = File.Create(to);
		await using var gzip = new GZipStream(output, CompressionLevel.SmallestSize);
		await input.CopyToAsync(gzip, token);
	}

	public static async Task Decompress(string from, string to, CancellationToken token)
	{
		await using var input = File.OpenRead(from);
		await using var output = File.Create(to);
		await using var gzip = new GZipStream(input, CompressionMode.Decompress);
		await gzip.CopyToAsync(output, token);
	}

	public async ValueTask HandleImage(string path, bool compress, CancellationToken token)
	{
		try
		{
			var ext = compress ? EXT_COMP : EXT_DAT;
			if (!File.Exists(path))
			{
				_logger.LogInformation("Could not find path: {Path}", path);
				return;
			}

			var compressedPath = Path.ChangeExtension(path, ext);
			await (compress 
				? Compress(path, compressedPath, token) 
				: Decompress(path, compressedPath, token));

			File.Delete(path);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to compress image at path: {Path}", path);
		}
	}

	public override async Task<bool> Execute(CompressCache options, CancellationToken token)
	{
		options.Directory ??= _cache.StoragePath;
		if (!Directory.Exists(options.Directory))
		{
			_logger.LogWarning("Cache path does not exist: {Path}", options.Directory);
			return false;
		}

		var compress = options.Mode?.ToLower() != "decompress";
		var extToFind = compress ? EXT_DAT : EXT_COMP;
		var images = Directory.GetFiles(options.Directory,
			$"*.{extToFind}", SearchOption.AllDirectories)
			.Distinct()
			.ToArray();
		if (images.Length == 0)
		{
			_logger.LogWarning("No images found in cache directory: {Path}", options.Directory);
			return true;
		}

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 5),
			CancellationToken = token
		};
		await Parallel.ForEachAsync(images, opts, (i, t) => HandleImage(i, compress, t));

		_logger.LogInformation("Finished {Mode} {Image} images in cache directory: {Path}", 
			compress ? "compressing" : "decompressing",
			images.Length, options.Directory);
		return true;
	}
}
