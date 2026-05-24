BEGIN;
    DROP MATERIALIZED VIEW IF EXISTS mb_tag_ext;
    CREATE MATERIALIZED VIEW mb_tag_ext AS
    WITH tag_sources AS (
        SELECT
            mt.tag_id,
            MAX(s.id::text)::uuid as source_id,
            COUNT(DISTINCT s.id) as source_count,
            SUM(CASE
                WHEN m.content_rating = 3 THEN 1
                WHEN m.nsfw = TRUE THEN 1
                ELSE 0
            END) as porn_count,
            SUM(CASE
                WHEN m.content_rating = 2 THEN 1
                ELSE 0
            END) as erotica_count,
            SUM(CASE
                WHEN m.content_rating = 1 THEN 1
                ELSE 0
            END) as suggestive,
            SUM (CASE
                WHEN m.content_rating = 0 THEN 1
                ELSE 0
            END) as safe_count,
            COUNT(DISTINCT m.id) as manga_count
        FROM mb_manga_tags mt
        JOIN mb_manga m ON mt.manga_id = m.id
        JOIN mb_sources s ON s.id = m.source_id
        WHERE
            mt.deleted_at IS NULL AND
            m.deleted_at IS NULL AND
            s.deleted_at IS NULL
        GROUP BY mt.tag_id
    )
    SELECT
        tag_id as tag_id,
        source_count > 1 as shared,
        (CASE
            WHEN porn_count = manga_count THEN 3
            WHEN erotica_count = manga_count THEN 2
            WHEN suggestive = manga_count THEN 1
            WHEN safe_count = manga_count THEN 0
        END) as restrict_content_rating,
        (CASE WHEN source_count = 1 THEN source_id END) as unique_source_id,
        manga_count as manga_count
    FROM tag_sources
    WHERE manga_count > 0;
    CREATE INDEX idx_tag_ext_id ON mb_tag_ext (tag_id);
COMMIT;