namespace MangaBox.Services;

/// <summary>
/// A service for bulk importing manga by URL from various sources
/// </summary>
public interface IBulkImportService
{
	/// <summary>
	/// Imports the manga from the given URLs
	/// </summary>
	/// <param name="profileId">The ID of the profile making the request</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <param name="urls">The URLs of the manga to import</param>
	/// <returns>The results of the request</returns>
	Task<Boxed> Import(Guid? profileId, CancellationToken token, params string[] urls);
}

internal class BulkImportService(
	IMangaLoaderService _loader,
	ILogger<BulkImportService> _logger) : IBulkImportService
{
	public async Task<Boxed> Import(Guid? profileId, CancellationToken token, params string[] urls)
	{
		var opts = new ParallelOptions
		{
			CancellationToken = token,
			MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4)
		};

		var output = new ConcurrentBag<BulkImportResult>();
		await Parallel.ForEachAsync(urls, opts, async (url, ct) =>
		{
			try
			{
				var result = await _loader.Load(profileId, url, false, ct);
				if (result.IsError<MangaBoxType<MbManga>>(out var error, out var data))
				{
					output.Add(new(url, null, error));
					return;
				}

				output.Add(new(url, data, null));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error importing manga from URL {Url}", url);
				output.Add(new(url, null, ex.Message));
			}
		});

		return Boxed.Ok(output.ToArray());
	}
}

/// <summary>
/// The result of a bulk import operation for a single URL
/// </summary>
/// <param name="Url">The URL of the manga</param>
/// <param name="Result">The result of the import operation</param>
/// <param name="Error">The error message, if any</param>
public record class BulkImportResult(
	[property: JsonPropertyName("url")] string Url,
	[property: JsonPropertyName("result")] MangaBoxType<MbManga>? Result,
	[property: JsonPropertyName("error")] string? Error);