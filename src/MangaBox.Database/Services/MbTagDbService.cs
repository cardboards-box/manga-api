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

    /// <summary>
    /// Merges alias tags into a remaining tag
    /// </summary>
    /// <param name="id">The tag ID to keep</param>
    /// <param name="aliases">The tag IDs to merge into the kept tag</param>
    /// <returns>The updated tag and deleted tag IDs</returns>
    Task<MbTagMergeResult> MergeAliases(Guid id, Guid[] aliases);
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

    public async Task<MbTagMergeResult> MergeAliases(Guid id, Guid[] aliases)
    {
        const string QUERY = """
            WITH kept_tag AS (
                SELECT id, slug, aliases
                FROM mb_tags
                WHERE
                    id = :id AND
                    deleted_at IS NULL
            ), alias_ids AS (
                SELECT DISTINCT alias_id AS id
                FROM unnest(CAST(:aliases AS uuid[])) AS alias_id
                WHERE alias_id <> :id
            ), tags_to_merge AS (
                SELECT t.id, t.slug, t.aliases
                FROM mb_tags t
                JOIN alias_ids a ON a.id = t.id
                WHERE t.deleted_at IS NULL
            )
            UPDATE mb_tags t
            SET
                aliases = COALESCE((
                    SELECT ARRAY_AGG(DISTINCT alias ORDER BY alias)
                    FROM (
                        SELECT unnest(COALESCE(t.aliases, '{}'::text[])) AS alias
                        UNION ALL
                        SELECT tm.slug AS alias
                        FROM tags_to_merge tm
                        UNION ALL
                        SELECT unnest(COALESCE(tm.aliases, '{}'::text[])) AS alias
                        FROM tags_to_merge tm
                    ) aliases
                    WHERE
                        alias IS NOT NULL AND
                        alias <> '' AND
                        alias <> t.slug
                ), '{}'::text[]),
                updated_at = CURRENT_TIMESTAMP
            WHERE
                t.id = (SELECT id FROM kept_tag);

            WITH kept_tag AS (
                SELECT id
                FROM mb_tags
                WHERE
                    id = :id AND
                    deleted_at IS NULL
            ), alias_ids AS (
                SELECT DISTINCT alias_id AS id
                FROM unnest(CAST(:aliases AS uuid[])) AS alias_id
                WHERE alias_id <> :id
            ), tags_to_merge AS (
                SELECT t.id
                FROM mb_tags t
                JOIN alias_ids a ON a.id = t.id
                WHERE t.deleted_at IS NULL
            )
            UPDATE mb_manga_tags keep_mt
            SET
                deleted_at = NULL,
                updated_at = CURRENT_TIMESTAMP
            FROM kept_tag k, tags_to_merge tm
            JOIN mb_manga_tags alias_mt ON alias_mt.tag_id = tm.id
            WHERE
                keep_mt.manga_id = alias_mt.manga_id AND
                keep_mt.tag_id = k.id AND
                keep_mt.deleted_at IS NOT NULL;

            WITH kept_tag AS (
                SELECT id
                FROM mb_tags
                WHERE
                    id = :id AND
                    deleted_at IS NULL
            ), alias_ids AS (
                SELECT DISTINCT alias_id AS id
                FROM unnest(CAST(:aliases AS uuid[])) AS alias_id
                WHERE alias_id <> :id
            ), tags_to_merge AS (
                SELECT t.id
                FROM mb_tags t
                JOIN alias_ids a ON a.id = t.id
                WHERE t.deleted_at IS NULL
            ), alias_manga_tags AS (
                SELECT
                    mt.id,
                    mt.manga_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY mt.manga_id
                        ORDER BY (mt.deleted_at IS NULL) DESC, mt.updated_at DESC, mt.id
                    ) AS rn
                FROM mb_manga_tags mt
                JOIN tags_to_merge tm ON tm.id = mt.tag_id
            ), duplicate_manga_tags AS (
                SELECT amt.id
                FROM alias_manga_tags amt
                WHERE
                    amt.rn > 1 OR
                    EXISTS (
                        SELECT 1
                        FROM mb_manga_tags keep_mt
                        WHERE
                            keep_mt.manga_id = amt.manga_id AND
                            keep_mt.tag_id = (SELECT id FROM kept_tag)
                    )
            )
            DELETE FROM mb_manga_tags mt
            USING duplicate_manga_tags dmt
            WHERE mt.id = dmt.id;

            WITH kept_tag AS (
                SELECT id
                FROM mb_tags
                WHERE
                    id = :id AND
                    deleted_at IS NULL
            ), alias_ids AS (
                SELECT DISTINCT alias_id AS id
                FROM unnest(CAST(:aliases AS uuid[])) AS alias_id
                WHERE alias_id <> :id
            ), tags_to_merge AS (
                SELECT t.id
                FROM mb_tags t
                JOIN alias_ids a ON a.id = t.id
                WHERE t.deleted_at IS NULL
            )
            UPDATE mb_manga_tags mt
            SET
                tag_id = (SELECT id FROM kept_tag),
                updated_at = CURRENT_TIMESTAMP
            FROM tags_to_merge tm
            WHERE
                mt.tag_id = tm.id AND
                EXISTS (SELECT 1 FROM kept_tag);

            WITH kept_tag AS (
                SELECT id
                FROM mb_tags
                WHERE
                    id = :id AND
                    deleted_at IS NULL
            ), alias_ids AS (
                SELECT DISTINCT alias_id AS id
                FROM unnest(CAST(:aliases AS uuid[])) AS alias_id
                WHERE alias_id <> :id
            ), tags_to_merge AS (
                SELECT t.id
                FROM mb_tags t
                JOIN alias_ids a ON a.id = t.id
                WHERE t.deleted_at IS NULL
            ), delete_tags AS (
                DELETE FROM mb_tags t
                USING tags_to_merge tm, kept_tag k
                WHERE t.id = tm.id
                RETURNING t.id
            )
            SELECT COALESCE(ARRAY_AGG(id), '{}'::uuid[]) AS deleted
            FROM delete_tags;

            REFRESH MATERIALIZED VIEW mb_tag_ext;
            
            SELECT t.*
            FROM mb_tags t
            WHERE 
                t.deleted_at IS NULL AND
                t.id = :id
            ORDER BY t.slug;

            SELECT e.*
            FROM mb_tag_ext e
            WHERE e.tag_id = :id;
            """;

        using var con = await _sql.CreateConnection();
        using var tran = con.BeginTransaction();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id, aliases = aliases ?? [] }, tran);

        var deleted = await rdr.ReadSingleAsync<Guid[]>();
        var item = await rdr.ReadSingleOrDefaultAsync<MbTag>();
        var ext = await rdr.ReadSingleOrDefaultAsync<MbTagExt>();

        if (item is null)
            throw new InvalidOperationException($"Tag {id} could not be found.");

        tran.Commit();

        return new MbTagMergeResult(
            new MangaBoxType<MbTag>(item, MangaBoxRelationship.FromEntity(ext)),
            deleted);
    }
}

/// <summary>
/// The result of merging multiple tags together
/// </summary>
/// <param name="Tag">The remaining tag that was merged into</param>
/// <param name="Deleted">All of the Id's of the mb_tags that were deleted</param>
public record class MbTagMergeResult(
    [property: JsonPropertyName("tag")] MangaBoxType<MbTag> Tag,
    [property: JsonPropertyName("deleted")] Guid[] Deleted);
