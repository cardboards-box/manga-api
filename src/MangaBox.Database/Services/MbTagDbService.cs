namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_tags table
/// </summary>
public interface IMbTagDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_tags table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbTag?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_tags table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbTag item);

    /// <summary>
    /// Updates a record in the mb_tags table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbTag item);

    /// <summary>
    /// Inserts a record in the mb_tags table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbTag item);

    /// <summary>
    /// Gets all of the records from the mb_tags table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbTag[]> Get();
}

internal class MbTagDbService(
    IOrmService orm) : Orm<MbTag>(orm), IMbTagDbService
{

}