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
	/// The queue for new chapters
	/// </summary>
	RedisQueue<MbChapter> NewChapters { get; }

	/// <summary>
	/// The queue for new images
	/// </summary>
	RedisQueue<QueueImage> NewImages { get; }

	/// <summary>
	/// The queue for new manga
	/// </summary>
	RedisQueue<MangaBoxType<MbManga>> NewManga { get; }
}

internal class MangaPublishService(
	IRedisService _redis,
	ILogger<MangaPublishService> _logger) : IMangaPublishService
{
	private const string CHANNEL_CHAPTER_NEW = "chapter:new";
	private const string CHANNEL_IMAGE_NEW = "image:new";
	private const string CHANNEL_MANGA_NEW = "manga:new";

	public RedisQueue<MbChapter> NewChapters => field ??= new(CHANNEL_CHAPTER_NEW, _redis, _logger, false);

	public RedisQueue<QueueImage> NewImages => field ??= new(CHANNEL_IMAGE_NEW, _redis, _logger, true);

	public RedisQueue<MangaBoxType<MbManga>> NewManga => field ??= new(CHANNEL_MANGA_NEW, _redis, _logger, false);
}

/// <summary>
/// Represents a redis queue
/// </summary>
/// <typeparam name="T">The type of item in the queue</typeparam>
/// <param name="Channel">The redis channel name</param>
/// <param name="Redis">The redis service</param>
/// <param name="Logger">The logger</param>
/// <param name="Background">Whether or not to background the task</param>
public record class RedisQueue<T>(
	string Channel,
	IRedisService Redis,
	ILogger Logger,
	bool Background)
{
	private bool _running = false;

	/// <summary>
	/// The queue of items
	/// </summary>
	public IRedisList<T> Queue => Redis.List<T>(Channel);

	/// <summary>
	/// The observable for when things get added to the list
	/// </summary>
	/// <returns>The observable</returns>
	public Task<IObservable<T?>> Observe() => Redis.Observe<T>(Channel);

	/// <summary>
	/// Add an item to the queue
	/// </summary>
	/// <param name="message">The item to add to the queue</param>
	public async Task Publish(T message)
	{
		await Queue.Append(message);
		await Redis.Publish(Channel, message);
	}

	/// <summary>
	/// Dequeues all items and executes the action
	/// </summary>
	/// <param name="action">The action to execute</param>
	/// <param name="token">The cancellation token</param>
	private async Task Trigger(Func<T, Task> action, CancellationToken token)
	{
		if (token.IsCancellationRequested || _running) return;

		_running = true;
		try
		{
			var any = false;
			while(!token.IsCancellationRequested)
			{
				var item = await Queue.Pop();
				if (item == null || token.IsCancellationRequested)
					break;

				any = true;

				if (!Background)
				{
					await action(item);
					continue;
				}

				_ = Task.Run(async () =>
				{
					try
					{
						await action(item);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "Error occurred while processing {Item} in {Channel}", item, Channel);
					}
				}, token);
			}

			if (!any) return;

			_running = false;
			await Trigger(action, token);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error processing queue {Channel}", Channel);
			throw;
		}
		finally
		{
			_running = false;
		}
	}

	/// <summary>
	/// Processes every item on the queue
	/// </summary>
	/// <param name="action">The action to run for every item</param>
	/// <param name="token">The cancellation token</param>
	public async Task Process(Func<T, Task> action, CancellationToken token)
	{
		try
		{
			var observe = await Observe();
			using var sub = observe.Subscribe(
				async (i) => await Trigger(action, token));

			await Trigger(action, token);
			await Task.Delay(Timeout.Infinite, token);
		}
		catch (OperationCanceledException) { }
		finally
		{
			_running = false;
		}
	}
}

/// <summary>
/// The contents of the image queue
/// </summary>
/// <param name="Id">The ID of the image</param>
/// <param name="Created">The date and time the image was queued</param>
/// <param name="Force">Whether or not to force indexing of the image</param>
public record class QueueImage(
	[property: JsonPropertyName("id")] Guid Id,
	[property: JsonPropertyName("created")] DateTime Created,
	[property: JsonPropertyName("force")] bool? Force = null);
