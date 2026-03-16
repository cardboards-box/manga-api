namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using System.Collections;

/// <summary>
/// The service for interacting with the mb_manga_progress table
/// </summary>
public interface IMbMangaProgressDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_manga_progress table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbMangaProgress?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_manga_progress table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbMangaProgress item);

    /// <summary>
    /// Updates a record in the mb_manga_progress table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbMangaProgress item);

    /// <summary>
    /// Inserts a record in the mb_manga_progress table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbMangaProgress item);

    /// <summary>
    /// Gets all of the records from the mb_manga_progress table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbMangaProgress[]> Get();

	/// <summary>
	/// Fetches a single progress by profile and manga ID
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="mangaId">The ID of the manga</param>
	/// <returns>The updated manga progress</returns>
	Task<MangaBoxType<MbMangaProgress>?> Fetch(Guid profileId, Guid mangaId);

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="profileId">The ID of the profile</param>
    /// <param name="ids">The IDs of the manga to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbMangaProgress>[]> FetchByManga(Guid profileId, params Guid[] ids);

	/// <summary>
	/// Updates the favourite status of a manga progress record
	/// </summary>
    /// <param name="profileId">The ID of the profile making the request</param>
    /// <param name="mangaId">The ID of the manga to favorite</param>
    /// <param name="favorite">The favorite status</param>
	/// <returns>The updated manga progress</returns>
	Task<MangaBoxType<MbMangaProgress>?> Favourite(Guid profileId, Guid mangaId, bool favorite);

	/// <summary>
	/// Force sets the progress for the given manga and profile
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="completed">Whether or not the manga has been read</param>
	/// <returns>The updated manga progress</returns>
	Task<MangaBoxType<MbMangaProgress>?> SetProgress(Guid profileId, Guid mangaId, bool completed);

    /// <summary>
    /// Updates the is_completed fields where necessary
    /// </summary>
    /// <returns>The progresses that were updated</returns>
    Task<MbMangaProgress[]> UpdateInProgress();
}

internal class MbMangaProgressDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbMangaProgress>(orm), IMbMangaProgressDbService
{
	public async Task<MangaBoxType<MbMangaProgress>?> Favourite(Guid profileId, Guid mangaId, bool favorite)
	{
        var query = await _cache.Required("upsert_favorite");
		return await FetchWithQuery(query, new
        {
            profileId,
			mangaId,
			favorite
		});
	}

    public async Task<MangaBoxType<MbMangaProgress>?> SetProgress(Guid profileId, Guid mangaId, bool completed)
    {
        var query = await _cache.Required("upsert_progress");
        return await FetchWithQuery(query, new 
        { 
            profileId, 
            mangaId, 
            completed 
        });
	}

    public Task<MangaBoxType<MbMangaProgress>?> Fetch(Guid profileId, Guid mangaId)
    {
        return FetchWithQuery(null, new { profileId, mangaId });
	}

    public async Task<MangaBoxType<MbMangaProgress>?> FetchWithQuery(string? query, object? pars)
    {
        const string BASE_QUERY = @"
SELECT 
    DISTINCT 
    COALESCE(p.id, m.id) as id,
    COALESCE(p.profile_id, :profileId) as profile_id,
    COALESCE(p.manga_id, m.id) as manga_id,
    p.last_read_ordinal,
    p.last_read_chapter_id,
    p.last_read_at,
    COALESCE(p.is_completed, false) as is_completed,
    COALESCE(p.favorited, false) as favorited,
    COALESCE(p.progress_percentage, 0) as progress_percentage,
    COALESCE(p.created_at, CURRENT_TIMESTAMP) as created_at,
    COALESCE(p.updated_at, CURRENT_TIMESTAMP) as updated_at,
    p.deleted_at
FROM mb_manga m
LEFT JOIN mb_manga_progress p ON 
    p.manga_id = m.id AND
    p.profile_id = :profileId AND
    p.deleted_at IS NULL
WHERE 
    m.id = :mangaId AND 
    m.deleted_at IS NULL;

SELECT DISTINCT c.*
FROM mb_chapter_progress c
JOIN mb_manga_progress p ON c.progress_id = p.id
JOIN mb_manga m ON p.manga_id = m.id
WHERE 
    p.manga_id = :mangaId AND 
    p.profile_id = :profileId AND
    p.deleted_at IS NULL AND
    c.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT DISTINCT l.*
FROM mb_lists l
JOIN mb_list_items i ON i.list_id = l.id
JOIN mb_manga m ON i.manga_id = m.id
WHERE
    i.manga_id = :mangaId AND
    l.profile_id = :profileId AND
    i.deleted_at IS NULL AND
    l.deleted_at IS NULL AND
    m.deleted_at IS NULL;";

        query = (query ?? "") + BASE_QUERY;

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, pars);

        var progress = await rdr.ReadSingleOrDefaultAsync<MbMangaProgress>();
        if (progress is null) return null;

		var results = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(results, await rdr.ReadAsync<MbChapterProgress>());
        MangaBoxRelationship.Apply(results, await rdr.ReadAsync<MbList>());

		return new(progress, [..results]);
	}

	public async Task<MangaBoxType<MbMangaProgress>[]> FetchByManga(Guid profileId, params Guid[] ids)
    {
        const string QUERY = @"
SELECT DISTINCT l.*
FROM mb_lists l
JOIN mb_list_items i ON i.list_id = l.id
JOIN mb_manga m ON i.manga_id = m.id
WHERE
    i.manga_id = ANY( :ids ) AND
    l.profile_id = :profileId AND
    i.deleted_at IS NULL AND
    l.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT 
    DISTINCT 
    i.manga_id as first_id,
    l.id as second_id
FROM mb_lists l
JOIN mb_list_items i ON i.list_id = l.id
JOIN mb_manga m ON i.manga_id = m.id
WHERE
    i.manga_id = ANY( :ids ) AND
    l.profile_id = :profileId AND
    i.deleted_at IS NULL AND
    l.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT 
    DISTINCT 
    COALESCE(p.id, m.id) as id,
    COALESCE(p.profile_id, :profileId) as profile_id,
    COALESCE(p.manga_id, m.id) as manga_id,
    p.last_read_ordinal,
    p.last_read_chapter_id,
    p.last_read_at,
    COALESCE(p.is_completed, false) as is_completed,
    COALESCE(p.favorited, false) as favorited,
    COALESCE(p.progress_percentage, 0) as progress_percentage,
    COALESCE(p.created_at, CURRENT_TIMESTAMP) as created_at,
    COALESCE(p.updated_at, CURRENT_TIMESTAMP) as updated_at,
    p.deleted_at
FROM mb_manga m
LEFT JOIN mb_manga_progress p ON 
    p.manga_id = m.id AND
    p.profile_id = :profileId AND
    p.deleted_at IS NULL
WHERE 
    m.id = ANY( :ids ) AND 
    m.deleted_at IS NULL;";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { profileId, ids });

        var lists = (await rdr.ReadAsync<MbList>()).ToDictionary(t => t.Id);
        var listMap = (await rdr.ReadAsync<IdMap>()).ToGDictionary(t => t.FirstId);

        var results = new List<MangaBoxType<MbMangaProgress>>();
        foreach(var prog in await rdr.ReadAsync<MbMangaProgress>())
        {
            var related = new List<MangaBoxRelationship>();
            var listIds = listMap.TryGetValue(prog.MangaId, out var lids) ? lids : [];
            foreach (var id in listIds)
                if (lists.TryGetValue(id.SecondId, out var list))
                    MangaBoxRelationship.Apply(related, list);
            results.Add(new(prog, [.. related]));
        }

        return [..results];
	}

    public async Task<MbMangaProgress[]> UpdateInProgress()
    {
        var query = await _cache.Required("update_in_progress");
        return await Get(query);
    }
}