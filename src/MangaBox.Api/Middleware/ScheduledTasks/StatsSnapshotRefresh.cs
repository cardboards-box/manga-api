namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for refreshing stats snapshots
/// </summary>
public class StatsSnapshotRefresh(
	IStatsService _stats,
	ILogger<StatsSnapshotRefresh> _logger) : IInvocable
{
	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			await _stats.TakeSnapshot();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Stats Snapshot Refresh] An error occurred while refreshing stats snapshots");
		}
	}
}
