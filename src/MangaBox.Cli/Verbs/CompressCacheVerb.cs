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
	public const int PRINT_AFTER = 1000;

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

	public async ValueTask<bool> HandleImage(string path, bool compress, CancellationToken token)
	{
		try
		{
			var ext = compress ? EXT_COMP : EXT_DAT;
			if (!File.Exists(path))
			{
				_logger.LogInformation("Could not find path: {Path}", path);
				return false;
			}

			var compressedPath = Path.ChangeExtension(path, ext);
			await (compress 
				? Compress(path, compressedPath, token) 
				: Decompress(path, compressedPath, token));

			File.Delete(path);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to compress image at path: {Path}", path);
			return false;
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
		int success = 0;
		int failed = 0;
		int total = 0;

		await Parallel.ForEachAsync(images, opts, async (i, t) =>
		{
			var worked = await HandleImage(i, compress, t);
			Interlocked.Increment(ref total);
			if (worked) Interlocked.Increment(ref success);
			else Interlocked.Increment(ref failed);

			if (total % PRINT_AFTER == 0)
			{
				_logger.LogInformation("Processed {Total}/{Count} ({Percentage:P2}) images. Success: {Success}, Failed: {Failed}",
					total, images.Length, (double)total / images.Length, success, failed);
			}
		});

		_logger.LogInformation("Finished {Mode} {Image} images (Succeeded: {Success}, Failed: {Failed}) in cache directory: {Path}", 
			compress ? "compressing" : "decompressing",
			images.Length, success, failed, options.Directory);
		return true;
	}
}
