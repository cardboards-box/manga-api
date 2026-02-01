namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_manga_tags table
/// </summary>
public interface IMbMangaTagDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_manga_tags table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbMangaTag?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_manga_tags table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbMangaTag item);

    /// <summary>
    /// Updates a record in the mb_manga_tags table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbMangaTag item);

    /// <summary>
    /// Inserts a record in the mb_manga_tags table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbMangaTag item);

    /// <summary>
    /// Gets all of the records from the mb_manga_tags table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbMangaTag[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbMangaTag>?> FetchWithRelationships(Guid id);
}

internal class MbMangaTagDbService(
    IOrmService orm) : Orm<MbMangaTag>(orm), IMbMangaTagDbService
{

    public async Task<MangaBoxType<MbMangaTag>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_manga_tags WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_manga p
JOIN mb_manga_tags c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT p.* 
FROM mb_tags p
JOIN mb_manga_tags c ON p.id = c.tag_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbMangaTag>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbTag>());

        return new MangaBoxType<MbMangaTag>(item, [..related]);
    }
}