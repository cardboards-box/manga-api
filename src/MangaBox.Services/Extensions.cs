namespace MangaBox.Services;

/// <summary>
/// Extensions for the services
/// </summary>
public static class Extensions
{
	/// <summary>
	/// Represents a grouping of items from an iterator
	/// </summary>
	/// <typeparam name="T">The type of items</typeparam>
	/// <param name="Items">The items</param>
	/// <param name="Last">The last item in the group that didn't match the selectors</param>
	/// <param name="Index">The index of the selector that didn't match</param>
	public record class Grouping<T>(T[] Items, T? Last, int Index);

	/// <summary>
	/// Moves the given iterator until if finds a selector that doesn't match
	/// </summary>
	/// <typeparam name="T">The type of data to process</typeparam>
	/// <param name="data">The iterator to process</param>
	/// <param name="previous">The last item for via any previous MoveUntil reference</param>
	/// <param name="selectors">The different properties to check against</param>
	/// <returns>All of the items in the current grouping</returns>
	public static Grouping<T> MoveUntil<T>(this IEnumerator<T> data, T? previous, params Func<T, object?>[] selectors)
	{
		var items = new List<T>();

		//Add the previous item to the collection of items
		if (previous != null) items.Add(previous);

		//Keep moving through the iterator until EoC
		while (data.MoveNext())
		{
			//Get the current item
			var current = data.Current;
			//Get the last item
			var last = items.LastOrDefault();

			//No last item? Add current and skip to next item
			if (last == null)
			{
				items.Add(current);
				continue;
			}

			//Iterate through selectors until one matches
			for (var i = 0; i < selectors.Length; i++)
			{
				//Get the keys to check
				var selector = selectors[i];
				var fir = selector(last);
				var cur = selector(current);

				//Check if the keys are the same
				var isSame = (fir == null && cur == null) ||
					(fir != null && fir.Equals(cur));

				//They are the same, move to next selector
				if (isSame) continue;

				//Break out of the check, returning the grouped items and the last item checked
				return new([..items], current, i);
			}

			//All selectors are the same, add item to the collection
			items.Add(current);
		}

		//Reached EoC, return items, no last, and no selector index
		return new([..items], default, -1);
	}

	/// <summary>
	/// Fetch an index via a predicate (or null if not found)
	/// </summary>
	/// <typeparam name="T">The type of data</typeparam>
	/// <param name="data">The data to process</param>
	/// <param name="predicate">The predicate used to find the index</param>
	/// <returns>The index or null</returns>
	public static int? IndexOfNull<T>(this IEnumerable<T> data, Func<T, bool> predicate)
	{
		var idx = data.IndexOf(predicate);
		return idx == -1 ? null : idx;
	}

	/// <summary>
	/// Fetch an index via a predicate
	/// </summary>
	/// <typeparam name="T">The type of data</typeparam>
	/// <param name="data">The data to process</param>
	/// <param name="predicate">The predicate used to find the index</param>
	/// <param name="start">The index to start at</param>
	/// <returns>The index or -1</returns>
	public static int IndexOf<T>(this IEnumerable<T> data, Func<T, bool> predicate, int start = 0)
	{
		int index = 0;
		foreach (var item in data)
		{
			if (index < start)
			{
				index++;
				continue;
			}

			if (predicate(item))
				return index;

			index++;
		}

		return -1;
	}

	/// <summary>
	/// Finds the index of the previous item that matches the predicate
	/// </summary>
	/// <typeparam name="T">The type of data</typeparam>
	/// <param name="data">The data to process</param>
	/// <param name="predicate">The predicate used to find the index</param>
	/// <param name="start">The index to start at</param>
	/// <returns>The index or -1</returns>
	public static int IndexOfBefore<T>(this IEnumerable<T> data, Func<T, bool> predicate, int start)
	{
		var items = data.Take(start).Reverse();
		int index = 0;
		foreach(var item in items)
		{
			if (predicate(item))
				return start - index - 1;

			index++;
		}

		return -1;
	}
}
