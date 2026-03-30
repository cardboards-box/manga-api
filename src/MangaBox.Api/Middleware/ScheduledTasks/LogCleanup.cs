namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A service to clean up old logs
/// </summary>
public class LogCleanup(
	IDbService _db,
	ILogger<LogCleanup> _logger) : IInvocable
{
	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			await _db.Log.CleanLogs();
			_logger.LogDebug("[Cleanup Logs] Cleaned up old logs successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Cleanup Logs] Error while cleaning up old logs");
		}
	}
}
