using CardboardBox.Redis;

namespace MangaBox.Api.Middleware;

/// <summary>
/// The background service for handling new chapters
/// </summary>
public class NewChapterBackgroundService(
	IRedisService _redis,
	IConfiguration _config,
	IMangaLoaderService _loader,
	IMangaPublishService _service,
	ILogger<NewChapterBackgroundService> _logger) : BackgroundService
{
	private const string MANGA_COOLDOWN_KEY = "manga:cooldown:{0}";

	/// <summary>
	/// The number of seconds to wait between allowing chapters to be pushed, to prevent spam
	/// </summary>
	public double CoolDownMin => double.TryParse(_config["NewChapterCooldownMin"], out var min) ? min : 60;

	/// <summary>
	/// Determine whether or not the chapter can be pushed
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <returns>Whether or not the chapter can be pushed</returns>
	public async Task<bool> CanPush(Guid mangaId)
	{
		var key = string.Format(MANGA_COOLDOWN_KEY, mangaId);
		var exists = await _redis.Get(key);
		if (exists is not null) return false;

		await _redis.Set(key, "1", TimeSpan.FromMinutes(CoolDownMin));
		return true;
	}

	/// <summary>
	/// Handles the new chapter being added to the queue
	/// </summary>
	/// <param name="chapter">The chapter to load</param>
	/// <param name="token">The cancellation token</param>
	public async Task HandleNewChapter(MbChapter chapter, CancellationToken token)
	{
		//Load all of the images for the chapter
		var response = await _loader.Pages(chapter.Id, false, token);
		if (response is null || !response.Success ||
			response is not Boxed<MangaBoxType<MbChapter>> fullChap)
		{
			var errors = string.Join("; ", response?.Errors ?? []).ForceNull() ?? "Unknown error";
			_logger.LogError("Failed to load chapter {ChapterId}: {Errors}", chapter.Id, errors);
			return;
		}

		if (!await CanPush(chapter.MangaId)) return;

		//Do the discord / push notification updates
	}

	/// <inheritdoc />
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting new chapter background service");
		return _service.NewChapters.Process(
			(c) => HandleNewChapter(c, stoppingToken), 
			stoppingToken);
	}
}
