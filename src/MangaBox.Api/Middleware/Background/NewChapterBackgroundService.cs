namespace MangaBox.Api.Middleware.Background;

/// <summary>
/// The background service for handling new chapters
/// </summary>
public class NewChapterBackgroundService(
	IMangaLoaderService _loader,
	IMangaPublishService _service,
	INotificationService _notifications,
	ILogger<NewChapterBackgroundService> _logger) : BackgroundService
{
	/// <summary>
	/// Handles the new chapter being added to the queue
	/// </summary>
	/// <param name="chapter">The chapter to load</param>
	/// <param name="token">The cancellation token</param>
	public async Task HandleNewChapter(MbChapter chapter, CancellationToken token)
	{
		try
		{
			//Load all of the images for the chapter
			var response = await _loader.Pages(chapter.Id, false, token);
			if (response is null || !response.Success ||
				response is not Boxed<MangaBoxType<MbChapter>> fullChap)
			{
				var errors = string.Join("; ", response?.Errors ?? []).ForceNull() ?? "Unknown error";
				_logger.LogError("[New Chapter Indexing] Failed to load chapter {ChapterId}: {Errors}", chapter.Id, errors);
				return;
			}

			if (!await _service.CanPushManga(chapter.MangaId)) return;

			if (!await _notifications.Chapter(chapter.Id, token))
				_logger.LogError("[New Chapter Indexing] Error sending push notifications for {ChapterId}", chapter.Id);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[New Chapter Indexing] Error handling new chapter {ChapterId}", chapter.Id);
		}
	}

	/// <inheritdoc />
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("[New Chapter Indexing] Starting new chapter background service");
		return _service.NewChapters.Process(
			(c) => HandleNewChapter(c, stoppingToken), 
			stoppingToken);
	}
}
