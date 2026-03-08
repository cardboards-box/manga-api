namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_list_items table
/// </summary>
public interface IMbListItemDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_list_items table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbListItem?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_list_items table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbListItem item);

	/// <summary>
	/// Updates a record in the mb_list_items table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbListItem item);

	/// <summary>
	/// Inserts a record in the mb_list_items table if it doesn't exist, otherwise updates it
	/// </summary>
	/// <param name="item">The item to update or insert</param>
	/// <returns>The ID of the inserted/updated record</returns>
	Task<Guid> Upsert(MbListItem item);

	/// <summary>
	/// Gets all of the records from the mb_list_items table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbListItem[]> Get();
}

internal class MbListItemDbService(
	IOrmService orm) : Orm<MbListItem>(orm), IMbListItemDbService
{

}