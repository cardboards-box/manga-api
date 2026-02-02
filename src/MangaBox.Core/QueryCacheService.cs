namespace MangaBox.Core;

/// <summary>
/// A service for caching SQL queries
/// </summary>
public interface IQueryCacheService
{
	/// <summary>
	/// Get a required query from the cache
	/// </summary>
	/// <param name="name">The name of the query</param>
	/// <param name="bust">Whether or not to reload the query</param>
	/// <returns>The query</returns>
	Task<string> Required(string name, bool bust = false) => Get(name, bust: bust, throwOnNotFound: true)!;

	/// <summary>
	/// Gets a query from the cache
	/// </summary>
	/// <param name="name">The name of the query</param>
	/// <param name="throwOnNotFound">Whether or not to throw an exception if the query was not found</param>
	/// <param name="bust">Whether or not to reload the query</param>
	/// <returns>The query</returns>
	Task<string?> Get(string name, bool throwOnNotFound = true, bool bust = false);
}

internal class QueryCacheService(
	ILogger<QueryCacheService> _logger) : IQueryCacheService
{
	private readonly ConcurrentDictionary<string, string> _queries = [];

	public async Task<string?> Get(string name, bool throwOnNotFound = true, bool bust = false)
	{
		if (!bust && _queries.TryGetValue(name, out var query))
			return query;

		var filename = Path.Combine($"{name}.sql".Split(['\\', '/']));
		var path = Path.Combine("Scripts", "Queries", filename).FindFile();
		if (string.IsNullOrEmpty(path))
		{
			if (throwOnNotFound)
				throw new FileNotFoundException($"Query '{name}' not found at '{path}'");

			_logger.LogWarning("Query '{name}' not found at '{path}'", name, path);
			return null;
		}

		return _queries[name] = await File.ReadAllTextAsync(path);
	}
}
