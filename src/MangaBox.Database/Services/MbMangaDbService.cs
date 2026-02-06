namespace MangaBox.Database.Services;

using Flurl;
using Models;
using Models.Composites;
using Models.Composites.Filters;

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
	/// Deletes a manga by it's ID (and all of it's chapters and images)
	/// </summary>
	/// <param name="id">The ID to delete</param>
	/// <returns>The number of records deleted</returns>
	Task<int> Delete(Guid id);

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

	/// <summary>
	/// Upserts the import manga JSON data
	/// </summary>
	/// <param name="sourceId">The source the manga was loaded from</param>
	/// <param name="json">The JSON data of the manga to upsert</param>
    /// <param name="profileId">The ID of the profile who created the JSON</param>
	/// <returns>The result of the upsert operation</returns>
	Task<UpsertResult?> UpsertJson(Guid sourceId, Guid? profileId, string json);

    /// <summary>
    /// Searches for a manga by the given filter
    /// </summary>
    /// <param name="filter">The manga filter</param>
    /// <returns>The searched manga</returns>
    Task<PaginatedResult<MangaBoxType<MbManga>>> Search(MangaSearchFilter filter);

    /// <summary>
    /// Fetches manga by their URLs
    /// </summary>
    /// <param name="urls">The urls to search for</param>
    /// <returns>The manga matching</returns>
    Task<MangaBoxType<MbManga>[]> ByUrls(params string[] urls);

    /// <summary>
    /// Fetches manga by source and Ids
    /// </summary>
    /// <param name="source">The source ID</param>
    /// <param name="mangaIds">The manga IDs</param>
    /// <returns>The manga matching</returns>
    Task<MangaBoxType<MbManga>[]> ByIds(Guid source, string[] mangaIds);
}

internal class MbMangaDbService(
    IOrmService orm,
    IQueryCacheService _cache) : Orm<MbManga>(orm), IMbMangaDbService
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

    public async Task<UpsertResult?> UpsertJson(Guid sourceId, Guid? profileId, string json)
    {
        var query = await _cache.Required("upsert_manga");
        using var con = await _sql.CreateConnection();
        var result = await con.ExecuteScalarAsync<string>(query, new
        {
            source_id = sourceId,
            profile_id = profileId,
            manga_json = json
        });
        if (string.IsNullOrEmpty(result))
            return null;

        return JsonSerializer.Deserialize<UpsertResult>(result);
	}

    public async Task<PaginatedResult<MangaBoxType<MbManga>>> Search(MangaSearchFilter filter)
    {
        var query = filter.Build(out var parameters);

        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(query, parameters);

        var total = await rdr.ReadSingleAsync<int>();
        if (total == 0) return new();
		var pages = (int)Math.Ceiling((double)total / filter.Size);

        var results = await FromMulti(rdr);
        return new(pages, total, results);
	}

    public static async Task<MangaBoxType<MbManga>[]> FromMulti(SqlMapper.GridReader rdr)
    {
		var images = (await rdr.ReadAsync<MbImage>()).ToGDictionary(t => t.MangaId);
		var extensions = (await rdr.ReadAsync<MbMangaExt>()).ToGDictionary(t => t.MangaId);
		var sources = (await rdr.ReadAsync<MbSource>()).ToDictionary(t => t.Id);
		var tags = (await rdr.ReadAsync<MbTag>()).ToDictionary(t => t.Id);
		var tagMap = (await rdr.ReadAsync<TagMap>()).ToGDictionary(t => t.MangaId);

		var results = new List<MangaBoxType<MbManga>>();

		foreach (var manga in await rdr.ReadAsync<MbManga>())
		{
			var related = new List<MangaBoxRelationship>();
			if (images.TryGetValue(manga.Id, out var imgs))
				MangaBoxRelationship.Apply(related, imgs);
			if (extensions.TryGetValue(manga.Id, out var exts))
				MangaBoxRelationship.Apply(related, exts);
			if (sources.TryGetValue(manga.SourceId, out var src))
				MangaBoxRelationship.Apply(related, src);
			if (tagMap.TryGetValue(manga.Id, out var tmap))
				MangaBoxRelationship.Apply(related, tmap
					.Select(t => tags.TryGetValue(t.TagId, out var tag) ? tag : null)
					.Where(t => t is not null));

			results.Add(new(manga, [.. related]));
		}
        return [.. results];
	}

	public override Task<int> Delete(Guid id)
	{
        const string QUERY = @"
UPDATE mb_manga SET deleted_at = CURRENT_TIMESTAMP WHERE id = :id;
UPDATE mb_chapters SET deleted_at = CURRENT_TIMESTAMP WHERE manga_id = :id;
UPDATE mb_images SET deleted_at = CURRENT_TIMESTAMP WHERE manga_id = :id;";
        return Execute(QUERY, new { id });
	}

	public async Task<MangaBoxType<MbManga>[]> ByUrls(params string[] urls)
	{
        urls = [..urls.Select(t => t.ToLower())];

        if (urls.Length == 0) return [];

        var suffix = SearchFilter<MangaOrderBy>.TableSuffix();

        string query = $@"BEGIN;
DROP TABLE IF EXISTS tmp_manga_results_{suffix};

CREATE TEMP TABLE tmp_manga_results_{suffix} ON COMMIT DROP AS
SELECT
    DISTINCT
    m.id
FROM mb_manga m
JOIN mb_chapters c ON m.id = c.manga_id
WHERE
    m.deleted_at IS NULL AND
    c.deleted_at IS NULL AND (
        LOWER(m.url) = ANY(:urls) OR
        LOWER(c.url) = ANY(:urls)
    );

SELECT i.*
FROM mb_images i
JOIN tmp_manga_results_{suffix} p ON p.id = i.manga_id
WHERE i.chapter_id IS NULL AND i.deleted_at IS NULL;

SELECT e.*
FROM mb_manga_ext e
JOIN tmp_manga_results_{suffix} p ON p.id = e.manga_id
WHERE e.deleted_at IS NULL;

SELECT DISTINCT s.*
FROM mb_sources s
JOIN mb_manga m ON m.source_id = s.id
JOIN tmp_manga_results_{suffix} p ON p.id = m.id
WHERE 
	m.deleted_at IS NULL AND
	s.deleted_at IS NULL;

SELECT DISTINCT t.*
FROM mb_tags t 
JOIN mb_manga_tags mt ON mt.tag_id = t.id
JOIN tmp_manga_results_{suffix} p ON p.id = mt.manga_id
WHERE t.deleted_at IS NULL AND mt.deleted_at IS NULL;

SELECT DISTINCT mt.manga_id, mt.tag_id
FROM mb_manga_tags mt
JOIN tmp_manga_results_{suffix} p ON p.id = mt.manga_id
WHERE mt.deleted_at IS NULL;

SELECT m.*
FROM mb_manga m
JOIN tmp_manga_results_{suffix} t ON m.id = t.id;

DROP TABLE tmp_manga_results_{suffix};
COMMIT;";

        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(query, new { urls });

        return await FromMulti(rdr);
	}

    public async Task<MangaBoxType<MbManga>[]> ByIds(Guid source, string[] mangaIds)
    {
		mangaIds = [.. mangaIds.Select(t => t.ToLower())];

		if (mangaIds.Length == 0) return [];

		var suffix = SearchFilter<MangaOrderBy>.TableSuffix();

		string query = $@"BEGIN;
DROP TABLE IF EXISTS tmp_manga_results_{suffix};

CREATE TEMP TABLE tmp_manga_results_{suffix} ON COMMIT DROP AS
SELECT
    DISTINCT
    m.id
FROM mb_manga m
WHERE
    m.source_id = :source AND
    LOWER(m.original_source_id) = ANY(:mangaIds) AND
    m.deleted_at IS NULL;

SELECT i.*
FROM mb_images i
JOIN tmp_manga_results_{suffix} p ON p.id = i.manga_id
WHERE i.chapter_id IS NULL AND i.deleted_at IS NULL;

SELECT e.*
FROM mb_manga_ext e
JOIN tmp_manga_results_{suffix} p ON p.id = e.manga_id
WHERE e.deleted_at IS NULL;

SELECT DISTINCT s.*
FROM mb_sources s
JOIN mb_manga m ON m.source_id = s.id
JOIN tmp_manga_results_{suffix} p ON p.id = m.id
WHERE 
	m.deleted_at IS NULL AND
	s.deleted_at IS NULL;

SELECT DISTINCT t.*
FROM mb_tags t 
JOIN mb_manga_tags mt ON mt.tag_id = t.id
JOIN tmp_manga_results_{suffix} p ON p.id = mt.manga_id
WHERE t.deleted_at IS NULL AND mt.deleted_at IS NULL;

SELECT DISTINCT mt.manga_id, mt.tag_id
FROM mb_manga_tags mt
JOIN tmp_manga_results_{suffix} p ON p.id = mt.manga_id
WHERE mt.deleted_at IS NULL;

SELECT m.*
FROM mb_manga m
JOIN tmp_manga_results_{suffix} t ON m.id = t.id;

DROP TABLE tmp_manga_results_{suffix};
COMMIT;";

		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, new { mangaIds, source });

		return await FromMulti(rdr);
	}

	public class TagMap
    {
        public Guid MangaId { get; set; }
        public Guid TagId { get; set; }
	}
}