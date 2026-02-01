namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the mb_profiles table
/// </summary>
public interface IMbProfileDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_profiles table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbProfile?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_profiles table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbProfile item);

    /// <summary>
    /// Updates a record in the mb_profiles table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbProfile item);

    /// <summary>
    /// Inserts a record in the mb_profiles table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbProfile item);

    /// <summary>
    /// Gets all of the records from the mb_profiles table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbProfile[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbProfile>?> FetchWithRelationships(Guid id);
}

internal class MbProfileDbService(
    IOrmService orm) : Orm<MbProfile>(orm), IMbProfileDbService
{

    public async Task<MangaBoxType<MbProfile>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_profiles WHERE id = :id AND deleted_at IS NULL;
SELECT DISTINCT c.*
FROM mb_profiles p
JOIN mb_manga_progress b ON b.profile_id = p.id
JOIN mb_manga c ON c.id = b.manga_id
WHERE 
    p.id = :id AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbProfile>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());

        return new MangaBoxType<MbProfile>(item, [..related]);
    }
}