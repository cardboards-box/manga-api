namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_manga_ext table
/// </summary>
public interface IMbMangaExtDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_manga_ext table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbMangaExt?> Fetch(Guid id);

    /// <summary>
    /// Gets all of the records from the mb_manga_ext table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbMangaExt[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbMangaExt>?> FetchWithRelationships(Guid id);

	/// <summary>
	/// Update the records for the given IDs
	/// </summary>
	/// <param name="ids">The IDs of the manga to update</param>
	/// <returns>The updated records</returns>
	Task<MbMangaExt[]> Update(params Guid[] ids);

    /// <summary>
    /// Mass updates all extension records
    /// </summary>
    /// <returns>The updated records</returns>
    /// <remarks>USE SPARINGLY</remarks>
    Task<MbMangaExt[]> MassUpdate();
}

internal class MbMangaExtDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbMangaExt>(orm), IMbMangaExtDbService
{

    public async Task<MbMangaExt[]> Update(params Guid[] ids)
    {
        var query = await _cache.Required("update_manga_ext");
        return await Get(query, new { ids });
    }

    public async Task<MbMangaExt[]> MassUpdate()
    {
		var query = await _cache.Required("update_manga_ext");
        query = query.Replace("m.id = ANY( :ids ) AND", "");
		return await Get(query);
	}

    public async Task<MangaBoxType<MbMangaExt>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_manga_ext WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_manga p
JOIN mb_manga_ext c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT p.* 
FROM mb_chapters p
JOIN mb_manga_ext c ON p.id = c.last_chapter_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbMangaExt>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbChapter>());

        return new MangaBoxType<MbMangaExt>(item, [..related]);
    }
}