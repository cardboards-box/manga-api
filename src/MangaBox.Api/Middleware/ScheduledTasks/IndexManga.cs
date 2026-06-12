namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for fetching new manga from sources
/// </summary>
public class IndexManga(
	IMangaLoaderService _loader,
	ILogger<IndexManga> _logger,
	LoaderSource _source) : ICancellableInvocable, IInvocable
{
	/// <inheritdoc />
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <inheritdoc />
	public async Task Invoke()
	{
		var name = _source.Service.GetType().Name;
		try
		{
			_logger.LogDebug("[Index Manga] Starting manga indexing for {Source}", name);
			await _loader.RunIndexer(_source, CancellationToken);
			_logger.LogDebug("[Index Manga] Finished manga indexing for {Source}", name);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Index Manga] Error while indexing for {Source}", name);
		}
	}
}
