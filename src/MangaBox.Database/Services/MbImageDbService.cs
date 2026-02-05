namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_images table
/// </summary>
public interface IMbImageDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_images table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbImage?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_images table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbImage item);

    /// <summary>
    /// Updates a record in the mb_images table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbImage item);

    /// <summary>
    /// Inserts a record in the mb_images table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbImage item);

    /// <summary>
    /// Gets all of the records from the mb_images table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbImage[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbImage>?> FetchWithRelationships(Guid id);

	/// <summary>
	/// Fetches a set of images by their IDs
	/// </summary>
	/// <param name="ids">The IDs of the images</param>
	/// <returns>The image set</returns>
	Task<MangaImageSet> FetchSet(params Guid[] ids);
}

internal class MbImageDbService(
    IOrmService orm) : Orm<MbImage>(orm), IMbImageDbService
{
    public async Task<MangaBoxType<MbImage>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * FROM mb_images WHERE id = :id AND deleted_at IS NULL;
SELECT p.* 
FROM mb_chapters p
JOIN mb_images c ON p.id = c.chapter_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT p.* 
FROM mb_manga p
JOIN mb_images c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT s.*
FROM mb_sources s
JOIN mb_manga p ON s.id = p.source_id
JOIN mb_images c ON p.id = c.manga_id
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL AND
    s.deleted_at IS NULL
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbImage>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbChapter>());
		MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbManga>());
        MangaBoxRelationship.Apply(related, await rdr.ReadAsync<MbSource>());

        return new MangaBoxType<MbImage>(item, [..related]);
    }

    public async Task<MangaImageSet> FetchSet(params Guid[] ids)
    {
        const string QUERY = @"SELECT DISTINCT * 
FROM mb_images 
WHERE 
    id = ANY( :ids ) AND 
    deleted_at IS NULL;

SELECT DISTINCT p.* 
FROM mb_manga p
JOIN mb_images c ON p.id = c.manga_id
WHERE 
    c.id = ANY( :ids ) AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;

SELECT DISTINCT s.*
FROM mb_sources s
JOIN mb_manga p ON s.id = p.source_id
JOIN mb_images c ON p.id = c.manga_id
WHERE 
    c.id = ANY( :ids ) AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL AND
    s.deleted_at IS NULL";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(QUERY, new { ids });

		var images = (await rdr.ReadAsync<MbImage>()).ToArray();
		var manga = (await rdr.ReadAsync<MbManga>()).ToArray();
        var sources = (await rdr.ReadAsync<MbSource>()).ToArray();
        return new(manga, sources, images);
	}
}