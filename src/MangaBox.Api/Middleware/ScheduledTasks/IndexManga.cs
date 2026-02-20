namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for fetching new manga from sources
/// </summary>
public class IndexManga(
	IMangaLoaderService _loader,
	ILogger<IndexManga> _logger) : ICancellableInvocable, IInvocable
{
	/// <inheritdoc />
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			await _loader.RunIndex(CancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Index Manga] Error while indexing manga");
		}
	}
}
