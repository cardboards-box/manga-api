WITH to_be_updated AS (
    SELECT
        p.id as progress_id,
        CAST(c.number as NUMERIC) / f.number * 100 as progress_percentage
    FROM mb_manga_progress p
    JOIN mb_manga_ext e ON e.manga_id = p.manga_id
    JOIN mb_vw_chapter_numbers f ON
        f.manga_id = e.manga_id AND
        f.ordinal = e.last_chapter_ordinal
    JOIN mb_vw_chapter_numbers c ON
        c.manga_id = e.manga_id AND
        c.ordinal = p.last_read_ordinal
    WHERE
        p.is_completed = TRUE AND
        p.last_read_ordinal < e.last_chapter_ordinal AND
        p.deleted_at IS NULL AND
        e.deleted_at IS NULL
), do_updated AS (
    UPDATE mb_manga_progress m
    SET
        progress_percentage = u.progress_percentage,
        is_completed = FALSE,
        updated_at = CURRENT_TIMESTAMP
    FROM to_be_updated u
    WHERE m.id = u.progress_id
)
SELECT DISTINCT
    p.*
FROM to_be_updated u
JOIN mb_manga_progress p ON p.id = u.progress_id
WHERE p.deleted_at IS NULL;