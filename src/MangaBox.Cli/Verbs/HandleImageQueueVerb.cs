using CardboardBox.Redis;

namespace MangaBox.Cli.Verbs;

using Database;
using Models;
using Match;
using Services;

[Verb("handle-image-queue", HelpText = "Handle the image queue.")]
internal class HandleImageQueueOptions
{
	[Option('p', "processor-count", HelpText = "Number of processors to use. Default is the number of logical processors minus one.", Default = -1)]
	public int ProcessorCount { get; set; } = -1;
}

internal class HandleImageQueueVerb(
	IRISIndexService _index,
	IMangaPublishService _publish,
	IMangaLoaderService _loader,
	ILogger<HandleImageQueueVerb> logger) : BooleanVerb<HandleImageQueueOptions>(logger)
{
	public IRedisList<QueueImage> ImageQueue => _publish.NewImages.Queue;
	public IRedisList<MbChapter> ChapterQueue => _publish.NewChapters.Queue;

	public async Task ProcessChapters(CancellationToken token)
	{
		while (true)
		{
			var chapter = await ChapterQueue.Pop();
			if (chapter is null) return;

			try
			{
				await _loader.Pages(chapter.Id, false, token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while handling chapter {ChapterId}", chapter.Id);
			}
		}
	}

	public async Task ProcessImages(CancellationToken token)
	{
		while(true)
		{
			var item = await ImageQueue.Pop();
			if (item is null) return;

			try
			{
				await _index.Index(item.Id, item.Force ?? false, token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while processing image {ImageId}", item.Id);
			}
		}
	}

	public override async Task<bool> Execute(HandleImageQueueOptions options, CancellationToken token)
	{
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = options.ProcessorCount > 0 ? options.ProcessorCount : Environment.ProcessorCount - 1,
			CancellationToken = token
		};
		var threads = Enumerable.Range(0, opts.MaxDegreeOfParallelism);
		_logger.LogInformation("Starting to process image queue with {ProcessorCount} processors", opts.MaxDegreeOfParallelism);
		await Parallel.ForEachAsync(threads, opts, async (thread, ct) =>
		{
			await (thread % 2 == 0 
				? ProcessImages(ct) 
				: ProcessChapters(ct));
		});
		_logger.LogInformation("Finished processing image queue");
		return true;
	}
}
