using CardboardBox.Redis;

namespace MangaBox.Database;

using Models;
using Models.Composites;

/// <summary>
/// A service for publishing updates to manga
/// </summary>
public interface IMangaPublishService
{
	/// <summary>
	/// Indicates that a new manga has been added
	/// </summary>
	/// <param name="manga">The manga that was added</param>
	Task MangaNew(MangaBoxType<MbManga> manga);

	/// <summary>
	/// Indicates that a manga has been updated
	/// </summary>
	/// <param name="manga">The manga that was updated</param>
	Task MangaUpdate(MangaBoxType<MbManga> manga);

	/// <summary>
	/// Indicates that a chapter has been added
	/// </summary>
	/// <param name="chapter">The chapter that was added</param>
	Task ChapterNew(MbChapter chapter);
}

internal class MangaPublishService(
	IRedisService _redis) : IMangaPublishService
{
	public Task ChapterNew(MbChapter chapter)
	{
		return _redis.Publish("chapter:new", chapter);
	}

	public Task MangaNew(MangaBoxType<MbManga> manga)
	{
		return _redis.Publish("manga:new", manga);
	}

	public Task MangaUpdate(MangaBoxType<MbManga> manga)
	{
		return _redis.Publish("manga:update", manga);
	}
}
