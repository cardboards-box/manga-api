CREATE OR REPLACE VIEW mb_vw_chapter_numbers AS
SELECT
    c.manga_id,
    c.ordinal,
    ROW_NUMBER() OVER (
        PARTITION BY c.manga_id
        ORDER BY c.ordinal
    ) as number
FROM mb_chapters c
WHERE c.deleted_at IS NULL
GROUP BY c.manga_id, c.ordinal;