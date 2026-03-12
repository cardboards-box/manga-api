using CardboardBox.Redis;

namespace MangaBox.Services.Queues;

using ChapterQueue = MultiRedisQueue<(MbChapter chap, MbSource? source), MbChapter, Guid>;

internal class MangaPublishService(
	IDbService _db,
	IRedisService _redis,
	ILogger<MangaPublishService> _logger) : IMangaPublishService
{
	private const string CHANNEL_CHAPTER_NEW = "chapter:new";
	private const string CHANNEL_IMAGE_NEW = "image:new";
	private const string CHANNEL_MANGA_NEW = "manga:new";

	//public IRedisQueue<(MbChapter chap, MbSource? source), MbChapter> NewChapters => field ??=
	//	new ChapterQueue(
	//		CHANNEL_CHAPTER_NEW, _redis, _logger, false,
	//		x =>
	//		{
	//			var key = x.source?.Id;
	//			return (key.HasValue, key.GetValueOrDefault());
	//		}, 
	//		x => x.chap, 
	//		() => _db.Source.Get().ContinueWith(t => t.Result.Select(t => t.Id).ToArray()));

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
