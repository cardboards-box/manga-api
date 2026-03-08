namespace MangaBox.Database.Services;

using MangaBox.Models.Composites;
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

	/// <summary>
	/// Request to create a link
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The response</returns>
	Task<MbListItemResponse?> Create(MbListItem.LinkRequest request);

	/// <summary>
	/// Request to delete a link
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The response</returns>
	Task<MbListItemResponse?> Delete(MbListItem.LinkRequest request);
}

internal class MbListItemDbService(
	IOrmService orm,
	IQueryCacheService _cache) : Orm<MbListItem>(orm), IMbListItemDbService
{
	public async Task<MbListItemResponse?> Create(MbListItem.LinkRequest request)
	{
		var query = await _cache.Required("list_item_create");
		return await _sql.Fetch<MbListItemResponse>(query, request);
	}

	public async Task<MbListItemResponse?> Delete(MbListItem.LinkRequest request)
	{
		var query = await _cache.Required("list_item_delete");
		return await _sql.Fetch<MbListItemResponse>(query, request);
	}
}