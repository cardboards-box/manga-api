namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_chapter_progress table
/// </summary>
public interface IMbChapterProgressDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_chapter_progress table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbChapterProgress?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_chapter_progress table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbChapterProgress item);

    /// <summary>
    /// Updates a record in the mb_chapter_progress table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbChapterProgress item);

    /// <summary>
    /// Inserts a record in the mb_chapter_progress table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbChapterProgress item);

    /// <summary>
    /// Gets all of the records from the mb_chapter_progress table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbChapterProgress[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbChapterProgress>?> FetchWithRelationships(Guid id);
}

internal class MbChapterProgressDbService(
    IOrmService orm) : Orm<MbChapterProgress>(orm), IMbChapterProgressDbService
{

    public async Task<MangaBoxType<MbChapterProgress>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_chapter_progress WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_manga_progress p
JOIN mb_chapter_progress c ON p.id = c.progress_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbChapterProgress>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbMangaProgress>());

        return new MangaBoxType<MbChapterProgress>(item, [..related]);
    }
}