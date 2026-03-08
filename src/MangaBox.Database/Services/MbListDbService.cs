namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_lists table
/// </summary>
public interface IMbListDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_lists table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbList?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_lists table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbList item);

	/// <summary>
	/// Updates a record in the mb_lists table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbList item);

	/// <summary>
	/// Inserts a record in the mb_lists table if it doesn't exist, otherwise updates it
	/// </summary>
	/// <param name="item">The item to update or insert</param>
	/// <returns>The ID of the inserted/updated record</returns>
	Task<Guid> Upsert(MbList item);

	/// <summary>
	/// Gets all of the records from the mb_lists table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbList[]> Get();

	/// <summary>
	/// Fetches the record and all related records
	/// </summary>
	/// <param name="id">The ID of the record to fetch</param>
	/// <returns>The record and all related records</returns>
	Task<MangaBoxType<MbList>?> FetchWithRelationships(Guid id);

	/// <summary>
	/// Fetches the list by it's name and the profile who owns it
	/// </summary>
	/// <param name="name">The name of the list</param>
	/// <param name="profileId">The ID of the profile who owns the list</param>
	/// <returns>The list if found, otherwise null</returns>
	Task<MbList?> Fetch(string name, Guid profileId);
}

internal class MbListDbService(
	IOrmService orm) : Orm<MbList>(orm), IMbListDbService
{
	public Task<MbList?> Fetch(string name, Guid profileId)
	{
		const string QUERY = @"
SELECT * 
FROM mb_lists 
WHERE 
	LOWER(name) = LOWER(:name) AND 
	profile_id = :profileId AND 
	deleted_at IS NULL;";
		return Fetch(QUERY, new { name, profileId });
	}

	public async Task<MangaBoxType<MbList>?> FetchWithRelationships(Guid id)
	{
		const string QUERY = @"
SELECT * FROM mb_lists WHERE id = :id AND deleted_at IS NULL;

SELECT p.* 
FROM mb_profiles p
JOIN mb_lists c ON p.id = c.profile_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT DISTINCT c.*
FROM mb_lists p
JOIN mb_list_items b ON b.list_id = p.id
JOIN mb_manga c ON c.id = b.manga_id
WHERE 
    p.id = :id AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

		var item = await rdr.ReadSingleOrDefaultAsync<MbList>();
		if (item is null) return null;

		var related = new List<MangaBoxRelationship>();
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbProfile>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());

		return new MangaBoxType<MbList>(item, [.. related]);
	}
}