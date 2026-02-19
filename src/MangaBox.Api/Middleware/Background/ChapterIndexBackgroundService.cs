namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for making sure chapters get indexed
/// </summary>
public class ChapterIndexBackgroundService(
	IDbService _db,
	IConfiguration _config,
	IMangaPublishService _publish,
	ILogger<ChapterIndexBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// The amount of time to delay between each indexing operation, in seconds
	/// </summary>
	public double DelaySec => double.TryParse(_config["ChapterReindexDelaySec"], out var sec) ? sec : 60 * 60 * 24;

	/// <summary>
	/// The amount of time to delay between each indexing operation
	/// </summary>
	public TimeSpan Delay => TimeSpan.FromSeconds(DelaySec);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[Chapter Indexing] Starting background service with a delay of {DelaySec} seconds", DelaySec);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var chapters = await _db.Chapter.GetZeroPageChapters();
				foreach (var chapter in chapters)
					await _publish.NewChapters.Publish(chapter);

				await Task.Delay(Delay, stoppingToken);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "[Chapter Indexing] Error while running background service");
				await Task.Delay(Delay, stoppingToken);
			}
		}
	}
}
