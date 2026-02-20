namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for refreshing manga
/// </summary>
public class MangaRefresh(
	IDbService _db,
	IMangaLoaderService _loader,
	ILogger<MangaRefresh> _logger) : ICancellableInvocable, IInvocable
{
	/// <inheritdoc />
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <summary>
	/// Refreshes the given manga
	/// </summary>
	/// <param name="manga">The manga to refresh</param>
	/// <param name="token">The cancellation token</param>
	public async ValueTask DoRefresh(MbManga manga, CancellationToken token)
	{
		try
		{
			var response = await _loader.Refresh(null, manga.Id, token);
			if (response is not null && response.Success) return;

			var errors = string.Join("; ", response?.Errors ?? [])?.ForceNull() ?? "Unknown error";
			_logger.LogWarning("[Refresh Service] Failed to refresh manga {MangaId}: {ErrorMessage}", manga.Id, errors);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Refresh Service] An error occurred while refreshing manga {MangaId}", manga.Id);
		}
	}

	/// <inheritdoc />
	public async Task Invoke()
	{
		try
		{
			var opts = new ParallelOptions
			{
				CancellationToken = CancellationToken,
				MaxDegreeOfParallelism = 4
			};
			var to = await _db.Manga.ToRefresh();
			await Parallel.ForEachAsync(to, opts, DoRefresh);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[Refresh Service] An error occurred during manga refresh");
		}
	}
}
