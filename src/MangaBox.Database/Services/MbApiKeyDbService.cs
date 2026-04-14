namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_api_keys table
/// </summary>
public interface IMbApiKeyDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_api_keys table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbApiKey?> Fetch(Guid id);

    /// <summary>
    /// Deletes the record with the given ID from the mb_api_keys table
    /// </summary>
    /// <param name="id">The ID of the record to delete</param>
    /// <returns>The number of records deleted</returns>
    Task<int> Delete(Guid id);

    /// <summary>
    /// Inserts a record into the mb_api_keys table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbApiKey item);

    /// <summary>
    /// Updates a record in the mb_api_keys table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbApiKey item);

    /// <summary>
    /// Inserts a record in the mb_api_keys table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbApiKey item);

    /// <summary>
    /// Gets all of the records from the mb_api_keys table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbApiKey[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbApiKey>?> FetchWithRelationships(Guid id);

    /// <summary>
    /// Fetches the record by its key and all related records
    /// </summary>
    /// <param name="key">The key of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbApiKey>?> FetchByKey(string key);

    /// <summary>
    /// Gets the API keys for a given profile
    /// </summary>
    /// <param name="pid">The ID of the profile</param>
    /// <returns>The API keys for the profile</returns>
    Task<MbApiKey[]> GetByProfile(Guid pid);
}

internal class MbApiKeyDbService(
    IOrmService orm) : Orm<MbApiKey>(orm), IMbApiKeyDbService
{
    public Task<MangaBoxType<MbApiKey>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_api_keys WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_profiles p
JOIN mb_api_keys c ON p.id = c.profile_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        return FetchMultiple(QUERY, new { id });
    }

    public Task<MangaBoxType<MbApiKey>?> FetchByKey(string key)
    {
        const string QUERY = @"SELECT * FROM mb_api_keys WHERE key = :key AND deleted_at IS NULL;
SELECT p.* 
FROM mb_profiles p
JOIN mb_api_keys c ON p.id = c.profile_id
WHERE 
    c.key = :key AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
";
        return FetchMultiple(QUERY, new { key });
    }

    public async Task<MangaBoxType<MbApiKey>?> FetchMultiple(string query, object? parameters = null)
    {
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(query, parameters);

        var item = await rdr.ReadSingleOrDefaultAsync<MbApiKey>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbProfile>());

        return new MangaBoxType<MbApiKey>(item, [..related]);
    }

    public Task<MbApiKey[]> GetByProfile(Guid pid)
    {
        const string QUERY = @"SELECT * FROM mb_api_keys WHERE profile_id = :pid AND deleted_at IS NULL;";
        return Get(QUERY, new { pid });
    }

    public override Task<int> Delete(Guid id)
    {
        const string QUERY = @"UPDATE mb_api_keys SET deleted_at = NOW() WHERE id = :id AND deleted_at IS NULL;";
        return Execute(QUERY, new { id });
    }
}