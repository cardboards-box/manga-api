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
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbMangaProgress>?> FetchWithRelationships(Guid id);
}

internal class MbMangaProgressDbService(
    IOrmService orm) : Orm<MbMangaProgress>(orm), IMbMangaProgressDbService
{
    public async Task<MangaBoxType<MbMangaProgress>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_manga_progress WHERE id = :id AND deleted_at IS NULL;

SELECT p.* 
FROM mb_manga p
JOIN mb_manga_progress c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT p.* 
FROM mb_chapters p
JOIN mb_manga_progress c ON p.id = c.last_read_chapter_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbMangaProgress>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbChapter>());

        return new MangaBoxType<MbMangaProgress>(item, [..related]);
    }
}