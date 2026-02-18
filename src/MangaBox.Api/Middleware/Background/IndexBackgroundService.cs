namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for indexing manga
/// </summary>
public class IndexBackgroundService(
	IConfiguration _config,
	IMangaLoaderService _loader,
	ILogger<IndexBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// The amount of time to delay between each indexing operation, in seconds
	/// </summary>
	public double DelaySec => double.TryParse(_config["MangaIndexDelaySec"], out var sec) ? sec : 30;

	/// <summary>
	/// The amount of time to delay between each indexing operation
	/// </summary>
	public TimeSpan Delay => TimeSpan.FromSeconds(DelaySec);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[Updates Indexing] Starting index loop with a delay of {DelaySec} seconds", DelaySec);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _loader.RunIndex(stoppingToken);
				await Task.Delay(Delay, stoppingToken);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "[Updates Indexing] An error occurred while running the index background service");
				await Task.Delay(Delay, stoppingToken);
			}
		}
	}
}
