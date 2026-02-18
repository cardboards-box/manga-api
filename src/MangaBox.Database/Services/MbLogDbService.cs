namespace MangaBox.Database.Services;

using Models;

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
}

internal class MbLogDbService(
	IOrmService orm) : Orm<MbLog>(orm), IMbLogDbService
{

}