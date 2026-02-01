namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_manga table
/// </summary>
public interface IMbMangaDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_manga table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbManga?> Fetch(Guid id);

	/// <summary>
	/// Fetches a manga by it's original source ID and source ID
	/// </summary>
	/// <param name="id">The original source ID of the manga</param>
	/// <param name="source">The source ID of the manga</param>
	/// <returns>The manga record</returns>
	Task<MangaBoxType<MbManga>?> FetchWithRelationships(string id, Guid source);

    /// <summary>
    /// Inserts a record into the mb_manga table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbManga item);

    /// <summary>
    /// Updates a record in the mb_manga table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbManga item);

    /// <summary>
    /// Inserts a record in the mb_manga table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbManga item);

    /// <summary>
    /// Gets all of the records from the mb_manga table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbManga[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbManga>?> FetchWithRelationships(Guid id);
}

internal class MbMangaDbService(
    IOrmService orm) : Orm<MbManga>(orm), IMbMangaDbService
{
	public async Task<MangaBoxType<MbManga>?> FetchWithRelationships(string id, Guid source)
	{
		const string QUERY = @"SELECT * FROM mb_manga WHERE original_source_id = :id AND source_id = :source AND deleted_at IS NULL;
SELECT p.* 
FROM mb_sources p
JOIN mb_manga c ON p.id = c.source_id
WHERE 
    c.original_source_id = :id AND
    c.source_id = :source AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT 
    DISTINCT 
    c.*,
    b.type
FROM mb_manga p
JOIN mb_manga_relationships b ON b.manga_id = p.id
JOIN mb_people c ON c.id = b.person_id
WHERE 
    p.original_source_id = :id AND
    p.source_id = :source AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;

SELECT DISTINCT c.*
FROM mb_manga p
JOIN mb_manga_tags b ON b.manga_id = p.id
JOIN mb_tags c ON c.id = b.tag_id
WHERE 
    p.original_source_id = :id AND
    p.source_id = :source AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;

SELECT DISTINCT e.*
FROM mb_manga_ext e
JOIN mb_manga m ON e.manga_id = m.id
WHERE 
    m.original_source_id = :id AND
    m.source_id = :source AND
    e.deleted_at IS NULL AND
    m.deleted_at IS NULL;

SELECT DISTINCT i.*
FROM mb_images i
JOIN mb_manga m ON i.manga_id = m.id
WHERE 
    m.original_source_id = :id AND
    m.source_id = :source AND
    i.chapter_id IS NULL AND
    i.deleted_at IS NULL AND
    m.deleted_at IS NULL;
";
		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY, new { id, source });

		var item = await rdr.ReadSingleOrDefaultAsync<MbManga>();
		if (item is null) return null;

		var related = new List<MangaBoxRelationship>();
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbSource>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbRelatedPerson>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbTag>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbMangaExt>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new MangaBoxType<MbManga>(item, [.. related]);
	}

	public async Task<MangaBoxType<MbManga>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_manga WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_sources p
JOIN mb_manga c ON p.id = c.source_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT 
    DISTINCT 
    c.*,
    b.type
FROM mb_manga p
JOIN mb_manga_relationships b ON b.manga_id = p.id
JOIN mb_people c ON c.id = b.person_id
WHERE 
    p.id = :id AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;

SELECT DISTINCT c.*
FROM mb_manga p
JOIN mb_manga_tags b ON b.manga_id = p.id
JOIN mb_tags c ON c.id = b.tag_id
WHERE 
    p.id = :id AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;

SELECT DISTINCT *
FROM mb_manga_ext 
WHERE 
    manga_id = :id AND 
    deleted_at IS NULL;

SELECT DISTINCT *
FROM mb_images
WHERE 
    manga_id = :id AND
    chapter_id IS NULL AND
    deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbManga>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbSource>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbRelatedPerson>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbTag>());
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbMangaExt>());
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbImage>());

		return new MangaBoxType<MbManga>(item, [..related]);
    }
}