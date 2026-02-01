namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_chapters table
/// </summary>
public interface IMbChapterDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_chapters table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbChapter?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_chapters table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbChapter item);

    /// <summary>
    /// Updates a record in the mb_chapters table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbChapter item);

    /// <summary>
    /// Inserts a record in the mb_chapters table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbChapter item);

    /// <summary>
    /// Gets all of the records from the mb_chapters table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbChapter[]> Get();

	/// <summary>
	/// Gets all of the chapters of a manga after a specific date
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="after">The date after which to get chapters</param>
	/// <returns>The chapters</returns>
	Task<MbChapter[]> Get(Guid mangaId, DateTime after);

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbChapter>?> FetchWithRelationships(Guid id);
}

internal class MbChapterDbService(
    IOrmService orm) : Orm<MbChapter>(orm), IMbChapterDbService
{
    public async Task<MangaBoxType<MbChapter>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_chapters WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_manga p
JOIN mb_chapters c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT DISTINCT *
FROM mb_images 
WHERE 
    chapter_id = :id AND 
    manga_id IS NULL AND
    deleted_at IS NULL;";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbChapter>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new MangaBoxType<MbChapter>(item, [..related]);
    }

	public Task<MbChapter[]> Get(Guid mangaId, DateTime after)
	{
		const string QUERY = @"SELECT * 
FROM mb_chapters 
WHERE 
    manga_id = :mangaId AND 
    created_at >= :after AND 
    deleted_at IS NULL
ORDER BY ordinal;";
        return Get(QUERY, new { mangaId, after });
	}
}