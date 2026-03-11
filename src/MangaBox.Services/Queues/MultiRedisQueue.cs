using CardboardBox.Redis;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace MangaBox.Services.Queues;

/// <summary>
/// Represents a redis queue with multiple underlying queues in Redis
/// </summary>
/// <typeparam name="TIn">The inbound item</typeparam>
/// <typeparam name="TOut">The item on the queue</typeparam>
/// <typeparam name="TKey">The type of key to use for identifying the queues</typeparam>
/// <param name="Channel">The redis channel name</param>
/// <param name="Redis">The redis service</param>
/// <param name="Logger">The logger</param>
/// <param name="Background">Whether or not to background the task</param>
/// <param name="Leases">The number of leases on the queue</param>
/// <param name="KeySelector">Extracts the key that identifies the queue to use for each item</param>
/// <param name="PreloadSources">A accessor for getting the sources to pre-populate the queues</param>
/// <param name="ValueSelector">Extracts the value from the input item</param>
public record class MultiRedisQueue<TIn, TOut, TKey>(
	string Channel,
	IRedisService Redis,
	ILogger Logger,
	bool Background,
	Func<TIn, (bool, TKey)> KeySelector,
	Func<TIn, TOut?> ValueSelector,
	Func<Task<TKey[]>> PreloadSources,
	int Leases = 10) : IRedisQueue<TIn, TOut>
		where TKey : notnull
{
	private bool _running = false;
	private readonly SemaphoreSlim _initSem = new(1, 1);
	private readonly CancellationTokenSource _cts = new();
	private readonly ConcurrentDictionary<TKey, QueueInfo> _queues = [];
	private readonly Subject<(TKey key, TOut item)> _queueSubject = new();

	/// <summary>
	/// The cancellation token for the request
	/// </summary>
	public CancellationToken Token => _cts.Token;

	/// <summary>
	/// Gets the queue for the given key
	/// </summary>
	/// <param name="key">The key of the queue</param>
	/// <returns>The queue information</returns>
	internal async Task<QueueInfo> GetQueue(TKey key)
	{
		if (_queues.TryGetValue(key, out var queue))
			return queue;

		var channel = $"{Channel}:{key}";
		var redis = Redis.List<TOut>(channel);
		var observe = await Redis.Observe<TOut>(channel);
		var sub = observe.Subscribe(async x =>
		{
			if (x is null) return;
			_queueSubject.OnNext((key, x));
		});
		var semaphore = new SemaphoreSlim(Leases, Leases);
		return _queues[key] = new(redis, observe, sub, channel, semaphore);
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<TOut> All([EnumeratorCancellation] CancellationToken token)
	{
		foreach(var queue in _queues.Values)
		{
			if (token.IsCancellationRequested) break;

			var items = await queue.Queue.All();
			foreach(var i in items)
			{
				if (token.IsCancellationRequested) break;
				yield return i;
			}
		}
	}

	/// <inheritdoc />
	public Task<IObservable<TOut?>> Observe()
	{
		return Task.FromResult(_queueSubject
			.Select(t => t.item)
			.AsObservable())!;
	}

	/// <inheritdoc />
	public async Task<long> Count()
	{
		long count = 0;
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = _queues.Count,
			CancellationToken = Token
		};
		await Parallel.ForEachAsync(_queues.Values, opts, async (queue, token) =>
		{
			var c = await queue.Queue.Length();
			Interlocked.Add(ref count, c);
		});
		return count;
	}

	/// <inheritdoc />
	public async Task Publish(TIn message)
	{
		var item = ValueSelector(message);
		if (item is null) return;

		var (found, key) = KeySelector(message);
		if (!found) return;

		var queue = await GetQueue(key);
		await queue.Queue.Append(item);
		await Redis.Publish(queue.Channel, item);
	}

	/// <summary>
	/// Dequeues items and processes them
	/// </summary>
	/// <param name="queue">The queue</param>
	/// <param name="action">The action to perform on each dequeued item</param>
	internal async Task Trigger(QueueInfo queue, Func<TOut, Task> action)
	{
		try
		{
			var any = false;
			while (!Token.IsCancellationRequested)
			{
				var item = await queue.Queue.Pop();
				if (item is null || Token.IsCancellationRequested)
					break;

				any = true;

				if (!Background)
				{
					await action(item);
					continue;
				}

				await queue.Sempahore.WaitAsync(Token);
				_ = Task.Run(async () =>
				{
					try
					{
						await action(item);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "Error occurred while processing {Item} in {Channel}", item, queue.Channel);
					}
					finally
					{
						queue.Sempahore.Release();
					}
				}, Token);
			}

			if (!any) return;

			await Trigger(queue, action);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error processing queue {Channel}", queue.Channel);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task Process(Func<TOut, Task> action, CancellationToken token)
	{
		try
		{
			if (_running)
				throw new InvalidOperationException("Processor is already running");

			_running = true;
			token.Register(_cts.Cancel);
			using var sub = _queueSubject.Subscribe(async (i) =>
			{
				if (!_queues.TryGetValue(i.key, out var queue)) return;

				await Trigger(queue, action);
			});

			var opts = new ParallelOptions
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount,
				CancellationToken = Token
			};
			await Parallel.ForEachAsync(_queues.Values, opts, async (queue, _) =>
			{
				await Trigger(queue, action);
			});
			
			await Task.Delay(Timeout.Infinite, Token);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error processing queues");
			throw;
		}
		finally
		{
			_running = false;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_cts.Cancel();
		foreach (var queue in _queues.Values)
		{
			queue.Subscription.Dispose();
			queue.Sempahore.Dispose();
		}
		_queues.Clear();
		_running = false;
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public async Task Remove(TOut item)
	{
		foreach(var queue in _queues.Values)
			await queue.Queue.Remove(item);
	}

	/// <inheritdoc />
	public async Task<TOut?> Pop()
	{
		foreach(var queue in _queues.Values)
		{
			var item = await queue.Queue.Pop();
			if (item is not null)
				return item;
		}

		return default;
	}

	/// <inheritdoc />
	public async Task Init()
	{
		if (!_queues.IsEmpty) return;

		try
		{
			await _initSem.WaitAsync(Token);

			if (!_queues.IsEmpty) return;

			var sources = await PreloadSources();
			foreach (var source in sources)
				await GetQueue(source);
		}
		finally
		{
			_initSem.Release();
		}
	}

	internal record class QueueInfo(
		IRedisList<TOut> Queue,
		IObservable<TOut?> Observable,
		IDisposable Subscription,
		string Channel,
		SemaphoreSlim Sempahore);
}
