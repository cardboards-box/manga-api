using CardboardBox.Redis;
using MangaBox.Database;

namespace MangaBox.Cli.Verbs;

[Verb("clear-image-queue", HelpText = "Clear the image queue.")]
internal class ClearImageQueueOptions
{

}

internal class ClearImageQueueVerb(
	ISqlService _sql,
	IRedisService _redis,
	ILogger<ClearImageQueueVerb> logger) : BooleanVerb<ClearImageQueueOptions>(logger)
{
	public IRedisList<QueueImage> ImageQueue => _redis.List<QueueImage>("image:new");

	public Task<Guid[]> LegacyImages()
	{
		const string QUERY = @"SELECT i.id
FROM mb_images i
JOIN mb_chapters c ON c.id = i.chapter_id
WHERE
    c.deleted_at IS NULL AND
    i.deleted_at IS NULL AND
    c.legacy_id IS NOT NULL AND
    c.legacy_id <= 0";
		return _sql.Get<Guid>(QUERY);
	}

	public override async Task<bool> Execute(ClearImageQueueOptions options, CancellationToken token)
	{
		var ids = await LegacyImages();
		_logger.LogInformation("Legacy Images: {Count}", ids.Length);

		var queued = await ImageQueue.All();
		int progress = 0;
		int removed = 0;

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token
		};

		await Parallel.ForEachAsync(queued, opts, async (item, ct) =>
		{
			Interlocked.Increment(ref progress); 
			if (progress % 1000 == 0)
				_logger.LogInformation("Progress: {Progress}/{Total} ({Percent:P2}%) - Removed: {Removed}",
					progress, queued.Length, (double)progress / queued.Length, removed);

			if (!ids.Contains(item.Id)) return;
			Interlocked.Increment(ref removed);
			await ImageQueue.Remove(item);
		});
		return true;
	}
}
