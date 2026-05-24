namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Types;

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

	/// <summary>
	/// Gets all of the records from the mb_tags table with their relationships
	/// </summary>
	/// <returns>All of the records with their relationships</returns>
	Task<MangaBoxType<MbTag>[]> GetWithRelationships(); 
}

internal class MbTagDbService(
	IOrmService orm) : Orm<MbTag>(orm), IMbTagDbService
{
	public async Task<MangaBoxType<MbTag>[]> GetWithRelationships()
	{
        const string QUERY = """
            SELECT 
                t.*, 
                '' as split, 
                e.*
            FROM mb_tags t
            JOIN mb_tag_ext e ON e.tag_id = t.id
            WHERE t.deleted_at IS NULL
            ORDER BY t.slug;
            """;
        return [..(await _sql.QueryTupleAsync<MbTag, MbTagExt>(QUERY))
            .Select(t => new MangaBoxType<MbTag>(t.item1, MangaBoxRelationship.FromEntity(t.item2)))];
	}
}