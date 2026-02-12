using System.Collections;

namespace MangaBox.Core;

/// <summary>
/// A thread-safe collection that has a maximum capacity.
/// </summary>
/// <typeparam name="T">The type of class</typeparam>
/// <param name="capacity">The max capacity of the collection</param>
public class CapacityCollection<T>(int capacity) : ICollection<T> where T : class
{
	private readonly ConcurrentQueue<T> _queue = [];
	private readonly int _capacity = capacity;

	/// <inheritdoc />
	public int Count => _queue.Count;

	/// <inheritdoc />
	public bool IsReadOnly => false;

	/// <inheritdoc />
	public void Add(T item)
	{
		while (_queue.Count > _capacity)
			_queue.TryDequeue(out _);

		_queue.Enqueue(item);
	}

	/// <inheritdoc />
	public void Clear()
	{
		_queue.Clear();
	}

	/// <inheritdoc />
	public bool Contains(T item)
	{
		return _queue.Contains(item);
	}

	/// <inheritdoc />
	public void CopyTo(T[] array, int arrayIndex)
	{
		_queue.CopyTo(array, arrayIndex);
	}

	/// <inheritdoc />
	public IEnumerator<T> GetEnumerator()
	{
		return _queue.GetEnumerator();
	}

	/// <inheritdoc />
	public bool Remove(T item)
	{
		return false;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
