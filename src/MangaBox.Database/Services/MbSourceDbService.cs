namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_sources table
/// </summary>
public interface IMbSourceDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_sources table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbSource?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_sources table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbSource item);

    /// <summary>
    /// Updates a record in the mb_sources table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbSource item);

    /// <summary>
    /// Inserts a record in the mb_sources table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbSource item);

    /// <summary>
    /// Gets all of the records from the mb_sources table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbSource[]> Get();
}

internal class MbSourceDbService(
    IOrmService orm) : CacheOrm<MbSource>(orm), IMbSourceDbService
{

}