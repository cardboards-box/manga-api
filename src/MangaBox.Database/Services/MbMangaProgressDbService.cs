namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

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
	Task<MbMangaProgress?> Favourite(Guid profileId, Guid mangaId, bool favorite);
}

internal class MbMangaProgressDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbMangaProgress>(orm), IMbMangaProgressDbService
{
	public async Task<MbMangaProgress?> Favourite(Guid profileId, Guid mangaId, bool favorite)
	{
        var query = await _cache.Required("upsert_favorite");
        return await Fetch(query, new
        {
            profileId,
            mangaId,
			favorite
		});
	}

	public async Task<MangaBoxType<MbMangaProgress>[]> FetchByManga(Guid profileId, params Guid[] ids)
    {
        const string QUERY = @"
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
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { ids, profileId });

        var chapters = (await rdr.ReadAsync<MbChapterProgress>()).ToGDictionary(t => t.ProgressId);
        var results = new List<MangaBoxType<MbMangaProgress>>();
        foreach(var progress in await rdr.ReadAsync<MbMangaProgress>())
        {
            var relationships = new List<MangaBoxRelationship>();
            MangaBoxRelationship.Apply(relationships, chapters.GetValueOrDefault(progress.Id, []));
            results.Add(new(progress, [.. relationships]));
		}

        return [.. results];
	}
}