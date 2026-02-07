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
		Guid[] ids = [mangaId];
		return (await FetchWithQuery(query, new
        {
            profileId,
			ids,
			favorite
		})).FirstOrDefault();
	}

    public async Task<MangaBoxType<MbMangaProgress>?> SetProgress(Guid profileId, Guid mangaId, bool completed)
    {
        var query = await _cache.Required("upsert_progress");
        Guid[] ids = [mangaId];
        return (await FetchWithQuery(query, new 
        { 
            profileId, 
            ids, 
            completed 
        })).FirstOrDefault();
	}

    public async Task<MangaBoxType<MbMangaProgress>?> Fetch(Guid profileId, Guid mangaId)
    {
        return (await FetchByManga(profileId, mangaId)).FirstOrDefault();
	}

    public async Task<MangaBoxType<MbMangaProgress>[]> FetchWithQuery(string? query, object? pars)
    {
        const string BASE_QUERY = @"
SELECT DISTINCT c.*
FROM mb_chapter_progress c
JOIN mb_manga_progress p ON c.progress_id = p.id
JOIN mb_manga m ON p.manga_id = m.id
WHERE 
    p.manga_id = ANY( :ids ) AND 
    p.profile_id = :profileId AND
    p.deleted_at IS NULL AND
    c.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT DISTINCT p.* 
FROM mb_manga_progress p
JOIN mb_manga m ON p.manga_id = m.id
WHERE 
    p.manga_id = ANY( :ids ) AND 
    p.profile_id = :profileId AND
    p.deleted_at IS NULL AND
    m.deleted_at IS NULL;";

        query = (query ?? "") + BASE_QUERY;

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, pars);

		var chapters = (await rdr.ReadAsync<MbChapterProgress>()).ToGDictionary(t => t.ProgressId);
		var results = new List<MangaBoxType<MbMangaProgress>>();
		foreach (var progress in await rdr.ReadAsync<MbMangaProgress>())
		{
			var relationships = new List<MangaBoxRelationship>();
			MangaBoxRelationship.Apply(relationships, chapters.GetValueOrDefault(progress.Id, []));
			results.Add(new(progress, [.. relationships]));
		}

		return [.. results];
	}

	public Task<MangaBoxType<MbMangaProgress>[]> FetchByManga(Guid profileId, params Guid[] ids)
    {
        return FetchWithQuery(null, new { profileId, ids });
	}

    public async Task<MbMangaProgress[]> UpdateInProgress()
    {
        var query = await _cache.Required("update_in_progress");
        return await Get(query);
    }
}