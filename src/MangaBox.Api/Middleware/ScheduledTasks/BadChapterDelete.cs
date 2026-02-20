namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A service for deleting any chapters where a 404 error occurred
/// </summary>
public class BadChapterDelete(
	IDbService _db,
	ILogger<BadChapterDelete> _logger) : IInvocable
{
	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			await _db.Chapter.Delete404Chapters();
			_logger.LogDebug("[Bad Chapter Delete] Deleted bad chapters successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Bad Chapter Delete] Error while deleting bad chapters");
		}
	}
}
