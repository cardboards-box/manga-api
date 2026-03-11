using CardboardBox.Redis;

namespace MangaBox.Services.Queues;

/// <summary>
/// Represents a redis queue with a single underlying queue in Redis
/// </summary>
/// <typeparam name="T">The type of item in the queue</typeparam>
/// <param name="Channel">The redis channel name</param>
/// <param name="Redis">The redis service</param>
/// <param name="Logger">The logger</param>
/// <param name="Background">Whether or not to background the task</param>
/// <param name="Leases">The number of leases on the queue</param>
public record class SingletonRedisQueue<T>(
	string Channel,
	IRedisService Redis,
	ILogger Logger,
	bool Background,
	int Leases = 10) : IRedisQueue<T>
{
	private bool _running = false;
	private readonly SemaphoreSlim _semaphore = new(Leases, Leases);
	private readonly CancellationTokenSource _cts = new();

	/// <summary>
	/// The cancellation token for the request
	/// </summary>
	public CancellationToken Token => _cts.Token;

	/// <summary>
	/// The queue of items
	/// </summary>
	public IRedisList<T> Queue => Redis.List<T>(Channel);

	/// <inheritdoc />
	public Task<long> Count() => Queue.Length();

	/// <inheritdoc />
	public Task<IObservable<T?>> Observe() => Redis.Observe<T>(Channel);

	/// <inheritdoc />
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
			while (!token.IsCancellationRequested)
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

				await _semaphore.WaitAsync(token);
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
					finally
					{
						_semaphore.Release();
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

	/// <inheritdoc />
	public async Task Process(Func<T, Task> action, CancellationToken token)
	{
		try
		{
			token.Register(_cts.Cancel);

			var observe = await Observe();
			using var sub = observe.Subscribe(
				async (i) => await Trigger(action, Token));

			await Trigger(action, Token);
			await Task.Delay(Timeout.Infinite, Token);
		}
		catch (OperationCanceledException) { }
		finally
		{
			_running = false;
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<T> All([EnumeratorCancellation] CancellationToken token)
	{
		var item = await Queue.All();
		foreach (var i in item)
		{
			if (token.IsCancellationRequested) 
				yield break;
			yield return i;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_cts.Cancel();
		_semaphore.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public Task Remove(T item) => Queue.Remove(item);

	/// <inheritdoc />
	public Task<T?> Pop() => Queue.Pop();

	/// <inheritdoc />
	public Task Init() => Task.CompletedTask;
}
