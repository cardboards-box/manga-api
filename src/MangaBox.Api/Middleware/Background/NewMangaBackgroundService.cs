namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for handling new manga
/// </summary>
public class NewMangaBackgroundService(
	IMangaPublishService _service, 
	INotificationService _notifications,
	ILogger<NewMangaBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// Handles the new manga being added to the queue
	/// </summary>
	/// <param name="manga">The manga being added</param>
	/// <param name="token">The cancellation token</param>
	public async Task HandleNewManga(MangaBoxType<MbManga> manga, CancellationToken token)
	{
		try
		{
			if (!await _service.CanPushManga(manga.Entity.Id)) return;

			if (!await _notifications.Manga(manga.Entity.Id, token))
				_logger.LogError("[New Manga Indexing] Error sending push notifications for {MangaId}", manga.Entity.Id);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[New Manga Indexing] Error handling new manga {MangaId}", manga.Entity.Id);
		}
	}

	/// <inheritdoc />
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[New Manga Indexing] Starting new manga background service");
		return _service.NewManga.Process(
			(m) => HandleNewManga(m, stoppingToken),
			stoppingToken);
	}
}
