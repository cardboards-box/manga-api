namespace MangaBox.Match;

using RIS;
using TaskType = Func<IImageSearchService, CancellationToken, IAsyncEnumerable<ImageSearchResult>>;

/// <summary>
/// The service for reverse image searching
/// </summary>
public interface IReverseImageSearchService
{
	/// <summary>
	/// Searches based on the given url
	/// </summary>
	/// <param name="url">The image url</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	Task<Boxed> Search(string url, CancellationToken token)
		=> Search(url, [RISServices.MatchRIS, RISServices.SauceNao, RISServices.GoogleVision], token);

	/// <summary>
	/// Searches based on the given url
	/// </summary>
	/// <param name="url">The image url</param>
	/// <param name="services">The services in the order to run them</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	Task<Boxed> Search(string url, RISServices[] services, CancellationToken token);

	/// <summary>
	/// Searches based on the given stream
	/// </summary>
	/// <param name="stream">The image stream</param>
	/// <param name="fileName">The image's name</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	Task<Boxed> Search(Stream stream, string fileName, CancellationToken token)
		=> Search(stream, fileName, [RISServices.MatchRIS, RISServices.SauceNao, RISServices.GoogleVision], token);

	/// <summary>
	/// Searches based on the given stream
	/// </summary>
	/// <param name="stream">The image stream</param>
	/// <param name="fileName">The image's name</param>
	/// <param name="services">The services in the order to run them</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The search results</returns>
	Task<Boxed> Search(Stream stream, string fileName, RISServices[] services, CancellationToken token);
}

internal class ReverseImageSearchService(
	IImageService _image,
	IEnumerable<IImageSearchService> _services,
	ILogger<ReverseImageSearchService> _logger) : IReverseImageSearchService
{
	public async Task<Boxed> Search(string url, RISServices[] services, CancellationToken token)
	{
		var image = await _image.Download(url, token);
		if (!string.IsNullOrEmpty(image.Error) || image.Stream is null)
			return Boxed.Exception(image.Error ?? "Image couldn't be downloaded!");

		using var stream = image.Stream;
		using var ms = await RISApiService.ToMemoryStream(stream);
		var fileName = image.FileName ?? "image.webp";
		return await Search((i, t) => i.Search(ms, fileName, t), services, token);
	}

	public async Task<Boxed> Search(Stream stream, string fileName, RISServices[] services, CancellationToken token)
	{
		using var ms = await RISApiService.ToMemoryStream(stream);
		return await Search((i, t) => i.Search(ms, fileName, t), services, token);
	}

	public async Task<Boxed> Search(TaskType action, RISServices[] services, CancellationToken token)
	{
		services = [..services.Distinct()];

		List<ImageSearchResult> results = [];
		List<string> errors = [];
		foreach(var ris in services)
		{
			var service = _services.FirstOrDefault(t => t.Type == ris);
			if (service is null)
				continue;

			try
			{
				await foreach (var result in action(service, token))
					results.Add(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while searching with {Service}", service.Type);
				errors.Add($"Error while searching with {service.Type}: {ex.Message}");
			}

			var matches = results.Any(t => t.Exact || t.Score > 80);
			if (matches)
				break;
		}

		var output = Boxed.Ok(results.OrderByDescending(t => t.Score).ToArray());
		output.Errors = [.. errors];
		return output;
	}
}
