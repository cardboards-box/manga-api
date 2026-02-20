namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for making sure images get indexed
/// </summary>
public class CatchupIndexingBackgroundService(
	IDbService _db,
	IConfiguration _config,
	IMangaPublishService _publish,
	ILogger<CatchupIndexingBackgroundService> _logger) : BackgroundService
{

	/// <summary>
	/// The amount of time to delay between each indexing operation, in seconds
	/// </summary>
	public double DelaySec => double.TryParse(_config["ImageReindexDelaySec"], out var sec) ? sec : 60;

	/// <summary>
	/// The amount of time to delay between each indexing operation
	/// </summary>
	public TimeSpan Delay => TimeSpan.FromSeconds(DelaySec);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[Catchup Indexing] Starting background service with a delay of {DelaySec} seconds", DelaySec);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var images = await _db.Image.NotIndexed();
				var queued = (await _publish.NewImages.Queue.All())
					.Select(x => x.Id)
					.Distinct()
					.ToHashSet();

				foreach (var image in images)
					if (!queued.Contains(image.Id))
						await _publish.NewImages.Publish(new(image.Id, DateTime.UtcNow, false));

				await Task.Delay(Delay, stoppingToken);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "[Catchup Indexing] Error while running background service");
				await Task.Delay(Delay, stoppingToken);
			}
		}
	}
}
