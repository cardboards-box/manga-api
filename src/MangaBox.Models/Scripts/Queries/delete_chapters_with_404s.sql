WITH chapters_404 AS (
    SELECT
        DISTINCT
        TRIM(substring(message FROM 'Fetch: (.*?)"')) as chapterId
    FROM mb_logs
    WHERE
        source = 'MangaBox.Utilities.MangaDex.MangaDexService' AND
        message LIKE 'Manga Dex Error: "Pages Fetch: %" - "not_found_http_exception%' AND
        created_at > (CURRENT_TIMESTAMP - INTERVAL '1 days')
), chapters_no_pages AS (
    SELECT
        DISTINCT
        TRIM(SUBSTRING(message, 24, 36))::uuid as chapterId
    FROM mb_logs
    WHERE
        category = 'New Chapter Indexing' AND
        log_level = 4 AND
        message LIKE '%No pages were found for chapter.%' AND
        created_at > (CURRENT_TIMESTAMP - INTERVAL '1 days')
), chapters_to_delete AS (
    SELECT
        c.id as chapter_id
    FROM mb_chapters c
    JOIN chapters_404 l ON l.chapterId = c.source_id
    JOIN mb_manga m ON m.id = c.manga_id
    WHERE
        c.deleted_at IS NULL AND
        m.deleted_at IS NULL
    UNION
    SELECT
        c.id as chapter_id
    FROM mb_chapters c
    JOIN chapters_no_pages l ON l.chapterId = c.id
    JOIN mb_manga m ON m.id = c.manga_id
    WHERE
        c.deleted_at IS NULL AND
        m.deleted_at IS NULL
), do_updates AS (
    UPDATE mb_chapters c
    SET deleted_at = CURRENT_TIMESTAMP
    WHERE c.id IN (
        SELECT chapter_id FROM chapters_to_delete
    )
)
SELECT * FROM chapters_to_delete