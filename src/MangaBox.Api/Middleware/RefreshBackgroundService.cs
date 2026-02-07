namespace MangaBox.Api.Middleware;

/// <summary>
/// A background service for refreshing manga
/// </summary>
public class RefreshBackgroundService(
	IDbService _db,
	IConfiguration _config,
	IMangaLoaderService _loader,
	ILogger<RefreshBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// The number of seconds to wait between refresh loops
	/// </summary>
	public double RefreshSec => double.TryParse(_config["MangaRefreshDelaySec"], out var sec) ? sec : 60;

	/// <summary>
	/// The delay to wait between refresh loops
	/// </summary>
	public TimeSpan RefreshDelay => TimeSpan.FromSeconds(RefreshSec);

	/// <summary>
	/// Refreshes the given manga
	/// </summary>
	/// <param name="manga">The manga to refresh</param>
	/// <param name="token">The cancellation token</param>
	public async ValueTask DoRefresh(MbManga manga, CancellationToken token)
	{
		var response = await _loader.Refresh(null, manga.Id, token);
		if (response is not null && response.Success) return;

		var errors = string.Join("; ", response?.Errors ?? [])?.ForceNull() ?? "Unknown error";
		_logger.LogWarning("Failed to refresh manga {MangaId}: {ErrorMessage}", manga.Id, errors);
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			_logger.LogInformation("Starting refresh loop with a delay of {DelaySec} seconds", RefreshSec);
			var opts = new ParallelOptions
			{
				CancellationToken = stoppingToken,
				MaxDegreeOfParallelism = 4
			};
			while(!stoppingToken.IsCancellationRequested)
			{
				var to = await _db.Manga.ToRefresh();
				await Parallel.ForEachAsync(to, opts, DoRefresh);

				await Task.Delay(RefreshDelay, stoppingToken);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while running the refresh background service");
			throw;
		}
	}
}
