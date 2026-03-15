namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_list_ext table
/// </summary>
public interface IMbListExtDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_list_ext table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbListExt?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_list_ext table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbListExt item);

	/// <summary>
	/// Updates a record in the mb_list_ext table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbListExt item);

	/// <summary>
	/// Inserts a record in the mb_list_ext table if it doesn't exist, otherwise updates it
	/// </summary>
	/// <param name="item">The item to update or insert</param>
	/// <returns>The ID of the inserted/updated record</returns>
	Task<Guid> Upsert(MbListExt item);

	/// <summary>
	/// Gets all of the records from the mb_list_ext table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbListExt[]> Get();

	/// <summary>
	/// Fetches the record and all related records
	/// </summary>
	/// <param name="id">The ID of the record to fetch</param>
	/// <returns>The record and all related records</returns>
	Task<MangaBoxType<MbListExt>?> FetchWithRelationships(Guid id);

	/// <summary>
	/// Updates all of the extended records for the given IDs, or everything if no IDs are given
	/// </summary>
	/// <param name="ids">The IDs of the records to update</param>
	/// <returns>The updated records</returns>
	Task<MbListExt[]> Update(params Guid[] ids);
}

internal class MbListExtDbService(
	IOrmService orm,
	IQueryCacheService _cache) : Orm<MbListExt>(orm), IMbListExtDbService
{
	public async Task<MangaBoxType<MbListExt>?> FetchWithRelationships(Guid id)
	{
		const string QUERY = @"SELECT * FROM mb_list_ext WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_lists p
JOIN mb_list_ext c ON p.id = c.list_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT p.* 
FROM mb_images p
JOIN mb_list_ext c ON p.id = c.cover_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

		var item = await rdr.ReadSingleOrDefaultAsync<MbListExt>();
		if (item is null) return null;

		var related = new List<MangaBoxRelationship>();
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbList>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new MangaBoxType<MbListExt>(item, [.. related]);
	}

	public async Task<MbListExt[]> Update(params Guid[] ids)
	{
		var query = await _cache.Required("update_list_ext");
		return await Get(query, new { ids });
	}
}
