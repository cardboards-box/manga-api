namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for taking stats snapshots
/// </summary>
public class StatsBackgroundService(
	IStatsService _stats,
	IConfiguration _config,
	ILogger<StatsBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// The amount of time to delay between each indexing operation, in seconds
	/// </summary>
	public double DelaySec => double.TryParse(_config["StatsDelaySec"], out var sec) ? sec : 30;

	/// <summary>
	/// The amount of time to delay between each indexing operation
	/// </summary>
	public TimeSpan Delay => TimeSpan.FromSeconds(DelaySec);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[Stats Refresh] Starting stats loop with a delay of {DelaySec} seconds", DelaySec);
		while(!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _stats.TakeSnapshot();
				await Task.Delay(Delay, stoppingToken);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "[Stats Refresh] An error occurred while running the stats background service");
				await Task.Delay(Delay, stoppingToken);
			}
		}
	}
}
