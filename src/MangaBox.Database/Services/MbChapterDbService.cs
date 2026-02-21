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
    /// Deletes a chapter by it's ID (and all of it's images)
    /// </summary>
    /// <param name="id">The ID of the chapter</param>
    /// <returns>The number of records deleted</returns>
    Task<int> Delete(Guid id);

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
    /// Gets all of the chapters for the given manga
    /// </summary>
    /// <param name="mangaId">The ID of the manga</param>
    /// <returns>The chapters</returns>
    Task<MbChapter[]> ByManga(Guid mangaId);

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbChapter>?> FetchWithRelationships(Guid id);

    /// <summary>
    /// Gets all of the 0 page chapters
    /// </summary>
    /// <returns>The chapters with 0 pages</returns>
    Task<MbChapter[]> GetZeroPageChapters();

    /// <summary>
    /// Delete all of the chapters where the pages have a 404 error on MD
    /// </summary>
    Task Delete404Chapters();
}

internal class MbChapterDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbChapter>(orm), IMbChapterDbService
{
    private string? _byManga;

	public Task<MbChapter[]> ByManga(Guid mangaId)
	{
        _byManga ??= Map.Select(t => t.With(a => a.MangaId));
        return Get(_byManga, new { MangaId = mangaId });
	}

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

SELECT DISTINCT s.*
FROM mb_sources s 
JOIN mb_manga m ON m.source_id = s.id
JOIN mb_chapters c ON c.manga_id = m.id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    m.deleted_at IS NULL AND
    s.deleted_at IS NULL;

SELECT DISTINCT *
FROM mb_images 
WHERE 
    chapter_id = :id AND
    deleted_at IS NULL
ORDER BY ordinal ASC;";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbChapter>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbSource>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new(item, [..related]);
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

    public override Task<int> Delete(Guid id)
    {
        const string QUERY = @"
UPDATE mb_chapters SET deleted_at = CURRENT_TIMESTAMP WHERE id = :id;
UPDATE mb_images SET deleted_at = CURRENT_TIMESTAMP WHERE chapter_id = :id;";
        return Execute(QUERY, new { id });
    }

	public Task<MbChapter[]> GetZeroPageChapters()
	{
        const string QUERY = @"SELECT DISTINCT c.*
FROM mb_chapters c
JOIN mb_manga m ON m.id = c.manga_id
LEFT JOIN mb_images i ON i.chapter_id = c.id AND i.manga_id = c.manga_id
WHERE
    c.page_count = 0 AND
    NULLIF(c.external_url, '') IS NULL AND
    i.id IS NULL AND
    c.deleted_at IS NULL AND
    m.deleted_at IS NULL;";
        return Get(QUERY);
	}

	public async Task Delete404Chapters()
	{
        var query = await _cache.Required("delete_chapters_with_404s");
        await Execute(query);
	}
}