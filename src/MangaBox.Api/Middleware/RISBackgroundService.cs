namespace MangaBox.Api.Middleware;

using Match;

/// <summary>
/// The background service for indexing images
/// </summary>
public class RISBackgroundService(
	IRISIndexService _index,
	IMangaPublishService _service,
	ILogger<RISBackgroundService> _logger) : BackgroundService
{
	/// <inheritdoc />
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting RIS indexing background service");
		return _service.NewImages.Process(async (image) =>
		{
			try
			{
				var response = await _index.Index(image.Id, stoppingToken);
				if (response is not null && response.Success) return;

				var errors = string.Join("; ", response?.Errors ?? [])?.ForceNull() ?? "Unknown error";
				_logger.LogWarning("Failed to index image {Id} in RIS: {Error}", image.Id, errors);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while indexing image: {id}", image.Id);
			}
		}, stoppingToken);
	}
}
