namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_people table
/// </summary>
public interface IMbPersonDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_people table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbPerson?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_people table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbPerson item);

    /// <summary>
    /// Updates a record in the mb_people table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbPerson item);

    /// <summary>
    /// Inserts a record in the mb_people table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbPerson item);

    /// <summary>
    /// Gets all of the records from the mb_people table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbPerson[]> Get();
}

internal class MbPersonDbService(
    IOrmService orm) : Orm<MbPerson>(orm), IMbPersonDbService
{

}