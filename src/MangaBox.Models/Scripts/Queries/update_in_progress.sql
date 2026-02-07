WITH to_be_updated AS (
    SELECT
        p.id as progress_id
    FROM mb_manga_progress p
    JOIN mb_manga_ext e ON e.manga_id = p.manga_id
    WHERE
        p.is_completed = TRUE AND
        p.last_read_ordinal < e.last_chapter_ordinal AND
        p.deleted_at IS NULL AND
        e.deleted_at IS NULL
), do_updated AS (
    UPDATE mb_manga_progress
    SET
        is_completed = FALSE,
        updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        SELECT progress_id
        FROM to_be_updated
    ) AND deleted_at IS NULL
)
SELECT DISTINCT
    p.*
FROM to_be_updated u
JOIN mb_manga_progress p ON p.id = u.progress_id
WHERE p.deleted_at IS NULL