using MangaBox.Database;

namespace MangaBox.Cli.Verbs;

[Verb("clear-chapter-queue", HelpText = "Clear the chapter queue.")]
internal class ClearChapterQueueOptions
{

}

internal class ClearChapterQueueVerb(
	IMangaPublishService _publish,
	ILogger<ClearChapterQueueVerb> logger) : BooleanVerb<ClearChapterQueueOptions>(logger)
{
	public override async Task<bool> Execute(ClearChapterQueueOptions options, CancellationToken token)
	{
		var chapters = new ConcurrentDictionary<Guid, byte>();

		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount,
			CancellationToken = token
		};

		var list = _publish.NewChapters.Queue;
		var queued = await list.All();
		int progress = 0;
		int removed = 0;
		await Parallel.ForEachAsync(queued, opts, async (chapter, ct) =>
		{
			Interlocked.Increment(ref progress);
			if (progress % 1000 == 0)
				_logger.LogInformation("Progress: {Progress}/{Total} ({Percent:P2}%) - Removed: {Removed}",
					progress, queued.Length, (double)progress / queued.Length, removed);

			if (!chapters.ContainsKey(chapter.Id))
			{
				chapters.TryAdd(chapter.Id, 0);
				return;
			}

			await list.Remove(chapter);
			Interlocked.Increment(ref removed);
		});
		_logger.LogInformation("Finished clearing chapter queue. Total: {Total}, Removed: {Removed}", queued.Length, removed);
		return true;
	}
}
