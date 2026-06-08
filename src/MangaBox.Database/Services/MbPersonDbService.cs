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

	/// <summary>
	/// Gets all of the records from the mb_people table
	/// </summary>
	/// <param name="ids">The IDs of the records</param>
	/// <returns>The records</returns>
	Task<MbPerson[]> Get(Guid[] ids);

	/// <summary>
	/// Searches the mb_people table for records matching the search query, and returns a paginated result
	/// </summary>
	/// <param name="search">The search query</param>
	/// <param name="page">The page number</param>
	/// <param name="size">The number of records per page</param>
	/// <param name="asc">Whether to sort in ascending order</param>
	/// <returns>A paginated result of matching records</returns>
	Task<PaginatedResult<MbPerson>> Search(string? search, int page, int size, bool asc);
}

internal class MbPersonDbService(
    IOrmService orm) : Orm<MbPerson>(orm), IMbPersonDbService
{
	public Task<MbPerson[]> Get(Guid[] ids)
	{
		return Get("SELECT * FROM mb_people WHERE id = ANY( :ids ) AND deleted_at IS NULL;", new { ids });
	}

	public Task<PaginatedResult<MbPerson>> Search(string? search, int page, int size, bool asc)
    {
        const string QUERY = """
            SELECT *
            FROM mb_people
            WHERE (
                :search IS NULL OR
                :search = '' OR
                fts @@ plainto_tsquery('english', :search)
            ) AND deleted_at IS NULL
            ORDER BY name {0}
            LIMIT :limit OFFSET :offset;

            SELECT COUNT(*) 
            FROM mb_people
            WHERE (
                :search IS NULL OR
                :search = '' OR
                fts @@ plainto_tsquery('english', :search)
            ) AND deleted_at IS NULL;
            """;

        var query = string.Format(QUERY, asc ? "ASC" : "DESC");
        return Paginate(query, new { search }, page, size);
	}
}