using CardboardBox.Redis;

namespace MangaBox.Services.Queues;

internal class MangaPublishService(
	IRedisService _redis,
	ILogger<MangaPublishService> _logger) : IMangaPublishService
{
	private const string CHANNEL_CHAPTER_NEW = "chapter:new";
	private const string CHANNEL_IMAGE_NEW = "image:new";
	private const string CHANNEL_MANGA_NEW = "manga:new";

	public IRedisQueue<MbChapter> NewChapters => field ??=
		new SingletonRedisQueue<MbChapter>(
			CHANNEL_CHAPTER_NEW, _redis, _logger, false);

	public IRedisQueue<QueueImage> NewImages => field ??=
		new SingletonRedisQueue<QueueImage>(
			CHANNEL_IMAGE_NEW, _redis, _logger, true);

	public IRedisQueue<MangaBoxType<MbManga>> NewManga => field ??= 
		new SingletonRedisQueue<MangaBoxType<MbManga>>(
			CHANNEL_MANGA_NEW, _redis, _logger, false);

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
