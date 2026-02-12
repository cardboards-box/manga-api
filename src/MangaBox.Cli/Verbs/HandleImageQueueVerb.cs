using CardboardBox.Redis;

namespace MangaBox.Cli.Verbs;

using Database;
using Match;

[Verb("handle-image-queue", HelpText = "Handle the image queue.")]
internal class HandleImageQueueOptions
{
	[Option('p', "processor-count", HelpText = "Number of processors to use. Default is the number of logical processors minus one.", Default = -1)]
	public int ProcessorCount { get; set; } = -1;
}

internal class HandleImageQueueVerb(
	IRedisService _redis,
	IRISIndexService _index,
	ILogger<HandleImageQueueVerb> logger) : BooleanVerb<HandleImageQueueOptions>(logger)
{
	public IRedisList<QueueImage> ImageQueue => _redis.List<QueueImage>("image:new");

	public async Task Process(CancellationToken token)
	{
		try
		{
			while(true)
			{
				var item = await ImageQueue.Pop();
				if (item is null) return;

				await _index.Index(item.Id, token);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while handling image queue");
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
			await Process(ct);
		});
		_logger.LogInformation("Finished processing image queue");
		return true;
	}
}
