WITH chapter_logs AS (
    SELECT
        substring(message FROM 'Fetch: (.*?)"') as chapterId
    FROM mb_logs
    WHERE
        source = 'MangaBox.Utilities.MangaDex.MangaDexService' AND
        message LIKE 'Manga Dex Error: "Pages Fetch: %" - "not_found_http_exception%'
    ORDER BY created_at DESC
), chapters_to_delete AS (
    SELECT
        c.id as chapter_id
    FROM mb_chapters c
    JOIN chapter_logs l ON l.chapterId = c.source_id
    JOIN mb_manga m ON m.id = c.manga_id
    WHERE c.deleted_at IS NULL AND m.deleted_at IS NULL
), do_updates AS (
    UPDATE mb_chapters c
    SET deleted_at = CURRENT_TIMESTAMP
    WHERE c.id IN (
        SELECT chapter_id FROM chapters_to_delete
    )
)
SELECT * FROM chapters_to_delete