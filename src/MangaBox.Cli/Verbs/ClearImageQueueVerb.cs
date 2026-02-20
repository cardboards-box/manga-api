using CardboardBox.Redis;
using MangaBox.Database;

namespace MangaBox.Cli.Verbs;

[Verb("clear-image-queue", HelpText = "Clear the image queue.")]
internal class ClearImageQueueOptions
{

}

internal class ClearImageQueueVerb(
	ISqlService _sql,
	IMangaPublishService _publish,
	ILogger<ClearImageQueueVerb> logger) : BooleanVerb<ClearImageQueueOptions>(logger)
{
	public IRedisList<QueueImage> ImageQueue => _publish.NewImages.Queue;

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

	public async Task Legacy(CancellationToken token)
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
	}

	public async Task Duplicates(CancellationToken token)
	{
		var items = new ConcurrentDictionary<Guid, byte>();

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token
		};

		var list = ImageQueue;
		var queued = await list.All();
		int progress = 0;
		int removed = 0;
		await Parallel.ForEachAsync(queued, opts, async (item, ct) =>
		{
			Interlocked.Increment(ref progress);
			if (progress % 1000 == 0)
				_logger.LogInformation("Progress: {Progress}/{Total} ({Percent:P2}%) - Removed: {Removed}",
					progress, queued.Length, (double)progress / queued.Length, removed);

			if (!items.ContainsKey(item.Id))
			{
				items.TryAdd(item.Id, 0);
				return;
			}

			await list.Remove(item);
			Interlocked.Increment(ref removed);
		});
		_logger.LogInformation("Finished clearing image queue. Total: {Total}, Removed: {Removed}", queued.Length, removed);
	}

	public override async Task<bool> Execute(ClearImageQueueOptions options, CancellationToken token)
	{
		await Duplicates(token);
		return true;
	}
}
