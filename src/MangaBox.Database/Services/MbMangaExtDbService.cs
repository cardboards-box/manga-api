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
    /// Inserts a record into the mb_manga_ext table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbMangaExt item);

    /// <summary>
    /// Updates a record in the mb_manga_ext table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbMangaExt item);

    /// <summary>
    /// Inserts a record in the mb_manga_ext table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbMangaExt item);

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
}

internal class MbMangaExtDbService(
    IOrmService orm) : Orm<MbMangaExt>(orm), IMbMangaExtDbService
{

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