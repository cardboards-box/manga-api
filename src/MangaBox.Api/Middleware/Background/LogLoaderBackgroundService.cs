namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// A service for loading logs into the DB
/// </summary>
public class LogLoaderBackgroundService(
	IDbService _db,
	ILogger<LogLoaderBackgroundService> _logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			_logger.LogInformation("[Log Loader] Starting log watching");
			var reader = DbLoggerSink.LogReader.ReadAllAsync(stoppingToken);
			await foreach (var log in reader)
				await _db.Log.Insert(log);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Log Loader] An exception occurred during log loading");
		}
		finally
		{
			DbLoggerSink.Finish();
		}
	}
}
