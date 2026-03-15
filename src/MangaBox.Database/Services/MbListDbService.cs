namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Composites.Filters;

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

	/// <summary>
	/// Searches for lists by the given filter
	/// </summary>
	/// <param name="filter">The search filter</param>
	/// <returns>The search results</returns>
	Task<PaginatedResult<MangaBoxType<MbList>>> Search(ListSearchFilter filter);

	/// <summary>
	/// Fetches all of the lists for a given profile
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <returns>All of the lists for the given profile</returns>
	Task<MbList[]> All(Guid? profileId);
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

	public Task<MbList[]> All(Guid? profileId)
	{
		const string QUERY = @"SELECT * 
FROM mb_lists 
WHERE 
	profile_id = :profileId AND 
	deleted_at IS NULL";
		return Get(QUERY, new { profileId });
	}

	public async Task<MangaBoxType<MbList>?> FetchWithRelationships(Guid id)
	{
		const string QUERY = @"
SELECT * FROM mb_lists WHERE id = :id AND deleted_at IS NULL;

SELECT DISTINCT p.*
FROM mb_lists l
JOIN mb_list_ext p ON p.list_id = l.id
WHERE 
	l.id = :id AND
	l.deleted_at IS NULL AND
	p.deleted_at IS NULL;

SELECT DISTINCT b.*
FROM mb_lists p
JOIN mb_list_items b ON b.list_id = p.id
WHERE 
    p.id = :id AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL;

SELECT DISTINCT a.*
FROM mb_lists l
JOIN mb_list_items i ON i.list_id = l.id
JOIN mb_manga m ON m.id = i.manga_id
JOIN mb_manga_tags t ON m.id = t.manga_id 
JOIN mb_tags a ON a.id = t.tag_id
WHERE
    l.id = :id AND
    l.deleted_at IS NULL AND
    i.deleted_at IS NULL AND
    t.deleted_at IS NULL AND
    a.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT DISTINCT p.*
FROM mb_lists l
JOIN mb_list_ext e ON e.list_id = l.id
JOIN mb_images p ON e.cover_id = p.id
WHERE
    l.id = :id AND
    l.deleted_at IS NULL AND
    e.deleted_at IS NULL AND
    p.deleted_at IS NULL AND
	p.last_failed_at IS NULL;";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

		var item = await rdr.ReadSingleOrDefaultAsync<MbList>();
		if (item is null) return null;

		var related = new List<MangaBoxRelationship>();
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbListExt>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbListItem>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbTag>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new MangaBoxType<MbList>(item, [.. related]);
	}

	public async Task<PaginatedResult<MangaBoxType<MbList>>> Search(ListSearchFilter filter)
	{
		var query = filter.Build(out var parameters);

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, parameters);

		var total = await rdr.ReadSingleAsync<int>();
		if (total == 0) return new();

		var pages = (int)Math.Ceiling((double)total / filter.Size);

		var ext = (await rdr.ReadAsync<MbListExt>()).ToDictionary(t => t.ListId);
		var tags = (await rdr.ReadAsync<MbTag>()).ToDictionary(t => t.Id);
		var tagMap = (await rdr.ReadAsync<IdMap>()).ToGDictionary(t => t.FirstId);
		var covers = (await rdr.ReadAsync<MbImage>()).ToDictionary(t => t.Id);

		var results = new List<MangaBoxType<MbList>>();

		foreach(var list in await rdr.ReadAsync<MbList>())
		{
			var related = new List<MangaBoxRelationship>();
			if (ext.TryGetValue(list.Id, out var e))
			{
				MangaBoxRelationship.Apply(related, e);
				if (e.CoverId is not null && covers.TryGetValue(e.CoverId.Value, out var cover))
					MangaBoxRelationship.Apply(related, cover);
			}
			if (tagMap.TryGetValue(list.Id, out var tmap))
				MangaBoxRelationship.Apply(related, tmap
					.Select(t => tags.TryGetValue(t.SecondId, out var tag) ? tag : null)
					.Where(t => t is not null));

			results.Add(new(list, [.. related]));
		}

		return new(pages, total, [.. results]);
	}
}