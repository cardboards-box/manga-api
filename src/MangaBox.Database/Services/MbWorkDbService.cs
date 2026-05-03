namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_works table
/// </summary>
public interface IMbWorkDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_works table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbWork?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_works table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbWork item);

	/// <summary>
	/// Updates a record in the mb_works table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbWork item);

	/// <summary>
	/// Gets all of the records from the mb_works table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbWork[]> Get();

	/// <summary>
	/// Links the manga together
	/// </summary>
	/// <param name="workId">The ID of the work group to use</param>
	/// <param name="ids">The IDs of the manga to link</param>
	Task LinkManga(Guid workId, Guid[] ids);

	/// <summary>
	/// Unlinks the manga from its work group
	/// </summary>
	/// <param name="id">The ID of the manga to unlink</param>
	Task UnlinkManga(Guid id);

	/// <summary>
	/// Clears any orphaned works.
	/// </summary>
	Task ClearOrphaned();
}

internal class MbWorkDbService(
	IOrmService orm) : Orm<MbWork>(orm), IMbWorkDbService
{
	public Task LinkManga(Guid workId, Guid[] ids)
	{
		const string QUERY = """
			UPDATE mb_manga 
			SET work_id = :workId
			WHERE 
				id = ANY(:ids) AND 
				deleted_at IS NULL
			""";
		return Execute(QUERY, new { workId, ids });
	}

	public Task UnlinkManga(Guid id)
	{
		const string QUERY = """
			UPDATE mb_manga 
			SET work_id = NULL
			WHERE 
				id = :id AND 
				deleted_at IS NULL
			""";
		return Execute(QUERY, new { id });
	}

	public Task ClearOrphaned()
	{
		const string QUERY = """
			DELETE FROM mb_works w
			WHERE
				NOT EXISTS (
					SELECT 1
					FROM mb_manga m
					WHERE
						m.work_id = w.id AND
						m.deleted_at IS NULL
				);
			""";
		return Execute(QUERY);
	}
}