namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for fetching missing pages for chapters
/// </summary>
public class FetchMissingPages(
	IDbService _db,
	IMangaPublishService _publish,
	ILogger<FetchMissingPages> _logger) : ICancellableInvocable, IInvocable
{
	/// <inheritdoc />
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			var chapters = await _db.Chapter.GetZeroPageChapters();
			var queued = await _publish.NewChapters.All(CancellationToken)
				.Select(x => x.Id)
				.Distinct()
				.ToHashSetAsync(null, CancellationToken);
			foreach (var (chapter, _) in chapters)
				if (!queued.Contains(chapter.Id))
					await _publish.NewChapters.Publish(chapter);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Fetch Missing Pages] Error while fetching missing pages");
		}
	}
}
