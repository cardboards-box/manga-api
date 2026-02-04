BEGIN;
DROP TABLE IF EXISTS migrate_progress;
DROP TABLE IF EXISTS migrate_progress_chapters;

CREATE TEMP TABLE migrate_progress ON COMMIT DROP AS
SELECT a.* FROM (
    SELECT profile_id, manga_id FROM manga_bookmarks
    UNION
    SELECT profile_id, manga_id FROM manga_favourites
    UNION
    SELECT profile_id, manga_id FROM manga_progress
) a
JOIN manga m ON a.manga_id = m.id
WHERE m.deleted_at IS NULL;

CREATE TEMP TABLE migrate_progress_chapters ON COMMIT DROP AS
SELECT * FROM (
    SELECT 
        p.profile_id, 
        p.manga_id, 
        p.manga_chapter_id as chapter_id 
    FROM manga_bookmarks p
    JOIN manga_chapter c ON p.manga_chapter_id = c.id
    WHERE 
        c.deleted_at IS NULL AND 
        p.deleted_at IS NULL

    UNION

    SELECT 
        p.profile_id, 
        p.manga_id, 
        p.manga_chapter_id as chapter_id 
    FROM manga_progress p
    JOIN manga_chapter c ON p.manga_chapter_id = c.id
    WHERE 
        c.deleted_at IS NULL AND 
        p.deleted_at IS NULL

    UNION

    SELECT
        p.profile_id,
        p.manga_id,
        (unnest(p.read)).chapter_id as chapter_id
    FROM manga_progress p
    JOIN manga_chapter c ON p.manga_chapter_id = c.id
    WHERE 
        c.deleted_at IS NULL AND 
        p.deleted_at IS NULL
) a;

SELECT
    a.profile_id,
    a.manga_id,
    c.ordinal as last_read_ordinal,
    c.id as last_read_chapter_id,
    (
        CASE WHEN e.completed = TRUE OR e.in_progress = TRUE THEN p.updated_at
             ELSE NULL
        END
    ) as last_read_at,
    COALESCE(e.completed, false) as is_completed,
    (
        CASE WHEN f.id IS NULL THEN FALSE
             ELSE TRUE
        END
    ) as favorited
FROM migrate_progress a
LEFT JOIN manga_progress_ext e ON
    e.manga_id = a.manga_id AND
    e.profile_id = a.profile_id
LEFT JOIN manga_chapter c ON c.id = e.progress_chapter_id
LEFT JOIN manga_progress p ON
    p.profile_id = a.profile_id AND
    p.manga_id = a.manga_id AND
    p.manga_chapter_id = e.progress_chapter_id
LEFT JOIN manga_favourites f ON
    f.profile_id = a.profile_id AND
    f.manga_id = a.manga_id;

WITH chap_progress AS (
    SELECT
        p.profile_id,
        p.manga_id,
        (unnest(p.read)).chapter_id as chapter_id,
        (unnest(p.read)).page_index + 1 as page_ordinal
    FROM manga_progress p
)
SELECT
    a.manga_id,
    a.profile_id,
    a.chapter_id,
    (
        CASE WHEN c.page_ordinal IS NOT NULL THEN c.page_ordinal
             WHEN p.page_index IS NULL THEN NULL
             ELSE p.page_index + 1
        END
    ) as page_ordinal,
    COALESCE(b.pages, '{}') as bookmarks,
    (
        CASE WHEN p.updated_at IS NULL THEN NULL
             WHEN c.chapter_id = p.manga_chapter_id THEN p.updated_at
             ELSE p.updated_at + INTERVAL '-1 day'
        END
    ) as last_read
FROM migrate_progress_chapters a
JOIN manga_chapter t ON t.id = a.chapter_id
LEFT JOIN manga_progress p ON
    a.manga_id = p.manga_id AND
    a.profile_id = p.profile_id
LEFT JOIN manga_bookmarks b ON
    a.manga_id = b.manga_id AND
    a.profile_id = b.profile_id AND
    a.chapter_id = b.manga_chapter_id
LEFT JOIN chap_progress c ON
    c.profile_id = a.profile_id AND
    c.manga_id = a.manga_id AND
    c.chapter_id = a.chapter_id
WHERE t.deleted_at IS NULL;
    
DROP TABLE IF EXISTS migrate_progress;
DROP TABLE IF EXISTS migrate_progress_chapters;
COMMIT;