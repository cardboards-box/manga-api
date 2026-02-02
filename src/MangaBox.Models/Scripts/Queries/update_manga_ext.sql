WITH manga_to_update AS (
    SELECT
        m.id,
        m.created_at
    FROM mb_manga m
    WHERE
        m.id = ANY( :ids ) AND
        m.deleted_at IS NULL
), chapter_grouped AS (
    SELECT
        m.id as manga_id,
        COUNT(c.ordinal) as chapter_count,
        COUNT(DISTINCT c.ordinal) as unique_chapter_count,
        MAX(c.ordinal) as last_chapter_ordinal,
        MIN(c.ordinal) as first_chapter_ordinal,
        MAX(c.created_at) as last_chapter_created,
        MIN(c.created_at) as first_chapter_created,
        COUNT(DISTINCT c.volume) as volume_count
    FROM mb_chapters c
    JOIN manga_to_update m ON m.id = c.manga_id
    WHERE c.deleted_at IS NULL
    GROUP BY m.id
), chapter_time_between_fp AS (
    SELECT
        c.manga_id,
        c.created_at,
        ROW_NUMBER() OVER (
            PARTITION BY c.manga_id, c.volume, c.ordinal
            ORDER BY c.created_at DESC
        ) as chapter_row
    FROM mb_chapters c
    JOIN manga_to_update m ON m.id = c.manga_id
    WHERE
        m.created_at <> c.created_at AND
        c.deleted_at IS NULL
), chapter_time_between AS (
    SELECT
         EXTRACT(EPOCH FROM (MAX(created_at) - MIN(created_at)) / nullif(COUNT(*) - 1, 0)) / 86400.0 as average,
         manga_id
    FROM chapter_time_between_fp
    WHERE chapter_row = 1
    GROUP BY manga_id
), progresses AS (
    SELECT
        m.id,
        SUM(CASE WHEN p.favorited = TRUE THEN 1 ELSE 0 END) as favourites,
        SUM(CASE WHEN p.last_read_ordinal IS NULL THEN 0 ELSE 1 END) as views
    FROM mb_manga_progress p
    JOIN manga_to_update m ON m.id = p.manga_id
    WHERE p.deleted_at IS NULL
    GROUP BY m.id
), chapter_grouped_addition AS (
    SELECT
        g.manga_id,
        g.chapter_count,
        g.unique_chapter_count,
        g.last_chapter_ordinal,
        g.first_chapter_ordinal,
        g.last_chapter_created,
        g.first_chapter_created,
        (
            SELECT c.id
            FROM mb_chapters c
            WHERE
                c.manga_id = g.manga_id AND
                c.ordinal = g.last_chapter_ordinal AND
                c.deleted_at IS NULL
            ORDER BY
                (CASE WHEN c.external_url IS NULL THEN 0 ELSE 1 END),
                c.created_at DESC
            LIMIT 1
        ) as last_chapter_id,
        (
            SELECT c.id
            FROM mb_chapters c
            WHERE
                c.manga_id = g.manga_id AND
                c.ordinal = g.first_chapter_ordinal AND
                c.deleted_at IS NULL
            ORDER BY
                (CASE WHEN c.external_url IS NULL THEN 0 ELSE 1 END),
                c.created_at
            LIMIT 1
        ) as first_chapter_id,
        g.volume_count,
        COALESCE(t.average, 0) as days_between_updates,
        COALESCE(f.views, 0) as views,
        COALESCE(f.favourites, 0) as favorites
    FROM chapter_grouped g
    LEFT JOIN chapter_time_between t ON t.manga_id = g.manga_id
    LEFT JOIN progresses f ON f.id = g.manga_id
)
INSERT INTO mb_manga_ext (
    manga_id,
    chapter_count,
    unique_chapter_count,
	last_chapter_ordinal,
	first_chapter_ordinal,
	last_chapter_created,
	first_chapter_created,
	last_chapter_id,
	first_chapter_id,
	volume_count,
	days_between_updates,
	views,
	favorites,
    created_at,
    updated_at
)
SELECT
    manga_id,
    chapter_count,
    unique_chapter_count,
	last_chapter_ordinal,
	first_chapter_ordinal,
	last_chapter_created,
	first_chapter_created,
	last_chapter_id,
	first_chapter_id,
	volume_count,
	days_between_updates,
	views,
	favorites,
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
FROM chapter_grouped_addition
ON CONFLICT (manga_id)
DO UPDATE SET
    chapter_count = EXCLUDED.chapter_count,
	unique_chapter_count = EXCLUDED.unique_chapter_count,
	last_chapter_ordinal = EXCLUDED.last_chapter_ordinal,
	first_chapter_ordinal = EXCLUDED.first_chapter_ordinal,
	last_chapter_created = EXCLUDED.last_chapter_created,
	first_chapter_created = EXCLUDED.first_chapter_created,
	last_chapter_id = EXCLUDED.last_chapter_id,
	first_chapter_id = EXCLUDED.first_chapter_id,
	volume_count = EXCLUDED.volume_count,
	days_between_updates = EXCLUDED.days_between_updates,
	views = EXCLUDED.views,
	favorites = EXCLUDED.favorites,
	updated_at = CURRENT_TIMESTAMP
RETURNING *;