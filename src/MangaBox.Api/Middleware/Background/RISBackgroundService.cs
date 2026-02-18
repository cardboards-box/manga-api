namespace MangaBox.Api.Middleware.Background;

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
		_logger.LogInformation("[RIS Indexing] Starting RIS indexing background service");
		return _service.NewImages.Process(async (image) =>
		{
			try
			{
				var response = await _index.Index(image.Id, image.Force ?? false, stoppingToken);
				if (response is not null && response.Success) return;

				var errors = string.Join("; ", response?.Errors ?? [])?.ForceNull() ?? "Unknown error";
				_logger.LogWarning("[RIS Indexing] Failed to index image {Id} in RIS: {Error}", image.Id, errors);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[RIS Indexing] Error occurred while indexing image: {id}", image.Id);
			}
		}, stoppingToken);
	}
}
