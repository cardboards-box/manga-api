namespace MangaBox.Database.Services;

using MangaBox.Models.Composites;
using Models;
using Models.Composites.Filters;

/// <summary>
/// The service for interacting with the mb_logs table
/// </summary>
public interface IMbLogDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_logs table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbLog?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_logs table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbLog item);

	/// <summary>
	/// Updates a record in the mb_logs table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbLog item);

	/// <summary>
	/// Gets all of the records from the mb_logs table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbLog[]> Get();

	/// <summary>
	/// Clean up old log files
	/// </summary>
	Task CleanLogs();

	/// <summary>
	/// Searches logs by the given filter
	/// </summary>
	/// <param name="filter">The search filter</param>
	/// <returns>The logs</returns>
	Task<PaginatedResult<MbLog>> Search(LogSearchFilter filter);

	/// <summary>
	/// The meta-data for logs
	/// </summary>
	/// <returns>The meta-data for logs</returns>
	Task<LogMetaData> MetaData();
}

internal class MbLogDbService(
	IOrmService orm,
	IQueryCacheService _cache) : Orm<MbLog>(orm), IMbLogDbService
{
	public async Task CleanLogs()
	{
		var query = await _cache.Required("clear_log_history");
		await Execute(query);
	}

	public async Task<PaginatedResult<MbLog>> Search(LogSearchFilter filter)
	{
		var query = filter.Build(out var pars);
		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, pars);

		var total = await rdr.ReadSingleAsync<int>();
		if (total == 0) return new();
		var pages = (int)Math.Ceiling((double)total / filter.Size);

		var results = await rdr.ReadAsync<MbLog>();
		return new(pages, total, [.. results]);
	}

	public async Task<LogMetaData> MetaData()
	{
		const string QUERY = @"
SELECT DISTINCT category 
FROM mb_logs 
WHERE 
	category IS NOT NULL AND 
	deleted_at IS NULL;

SELECT DISTINCT source 
FROM mb_logs 
WHERE 
	source IS NOT NULL AND 
	deleted_at IS NULL;";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY);

		var categories = await rdr.ReadAsync<string>();
		var sources = await rdr.ReadAsync<string>();
		return new([.. categories], [.. sources]);
	}
}