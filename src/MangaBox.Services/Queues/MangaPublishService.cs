using CardboardBox.Redis;

namespace MangaBox.Services.Queues;

internal class MangaPublishService(
	IRedisService _redis,
	IConfiguration _config,
	ILogger<MangaPublishService> _logger) : IMangaPublishService
{
	private const string CHANNEL_CHAPTER_NEW = "chapter:new";
	private const string CHANNEL_IMAGE_NEW = "image:new";
	private const string CHANNEL_MANGA_NEW = "manga:new";
	private const string MANGA_COOLDOWN_KEY = "manga:cooldown:{0}";

	public IRedisQueue<MbChapter> NewChapters => field ??=
		new SingletonRedisQueue<MbChapter>(
			CHANNEL_CHAPTER_NEW, _redis, _logger, false);

	public IRedisQueue<QueueImage> NewImages => field ??=
		new SingletonRedisQueue<QueueImage>(
			CHANNEL_IMAGE_NEW, _redis, _logger, true);

	public IRedisQueue<MangaBoxType<MbManga>> NewManga => field ??= 
		new SingletonRedisQueue<MangaBoxType<MbManga>>(
			CHANNEL_MANGA_NEW, _redis, _logger, false);

	/// <summary>
	/// The number of seconds to wait between allowing chapters to be pushed, to prevent spam
	/// </summary>
	public double CoolDownMin => double.TryParse(_config["NewChapterCooldownMin"], out var min) ? min : 60;

	public async Task<bool> CanPushManga(Guid mangaId)
	{
		var key = string.Format(MANGA_COOLDOWN_KEY, mangaId);
		var exists = await _redis.Get(key);
		if (exists is not null) return false;

		await _redis.Set(key, "1", TimeSpan.FromMinutes(CoolDownMin));
		return true;
	}

	public async Task Init()
	{
		await Task.WhenAll(
			NewChapters.Init(),
			NewImages.Init(),
			NewManga.Init());
	}

	public void Dispose()
	{
		NewChapters.Dispose();
		NewImages.Dispose();
		NewManga.Dispose();
	}
}
