namespace MangaBox.Database;

/// <summary>
/// Represents a redis queue
/// </summary>
/// <typeparam name="TIn">The inbound item</typeparam>
/// <typeparam name="TOut">The item on the queue</typeparam>
public interface IRedisQueue<TIn, TOut>	: IDisposable
{
	/// <summary>
	/// Fetches all items from the queue
	/// </summary>
	/// <param name="token">The cancellation token</param>
	/// <returns>Every item currently on the queue</returns>
	IAsyncEnumerable<TOut> All(CancellationToken token);

	/// <summary>
	/// Takes a single item from the queue
	/// </summary>
	/// <returns>The item on the queue</returns>
	Task<TOut?> Pop();

	/// <summary>
	/// The number of items currently in the queue
	/// </summary>
	/// <returns>The number of items in the queue</returns>
	Task<long> Count();

	/// <summary>
	/// Initialize the queue
	/// </summary>
	Task Init();

	/// <summary>
	/// The observable for when things get added to the list
	/// </summary>
	/// <returns>The observable</returns>
	Task<IObservable<TOut?>> Observe();

	/// <summary>
	/// Add an item to the queue
	/// </summary>
	/// <param name="message">The item to add to the queue</param>
	Task Publish(TIn message);

	/// <summary>
	/// Processes every item on the queue
	/// </summary>
	/// <param name="action">The action to run for every item</param>
	/// <param name="token">The cancellation token</param>
	Task Process(Func<TOut, Task> action, CancellationToken token);

	/// <summary>
	/// Remove an item from the queue
	/// </summary>
	/// <param name="item">The item to remove</param>
	/// <returns></returns>
	Task Remove(TOut item);
}

/// <summary>
/// Represents a redis queue
/// </summary>
/// <typeparam name="T">The type of item in the queue</typeparam>
public interface IRedisQueue<T> : IRedisQueue<T, T> { }