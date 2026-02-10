WITH manga_upserts AS (
    INSERT INTO mb_manga_progress
    (
        profile_id,
        manga_id,
        favorited,
        is_completed,
        progress_percentage,
        last_read_ordinal,
        last_read_chapter_id,
        last_read_at,
        updated_at,
        created_at
    )
    SELECT
        p.id as profile_id,
        m.id as manga_id,
        FALSE as favorited,
        :completed as is_completed,
        (
            CASE WHEN :completed = TRUE THEN 100
                 ELSE 0
            END
        ) as progress_percentage,
        (
            CASE WHEN :completed = TRUE THEN e.last_chapter_ordinal
                 ELSE NULL 
            END
        ) as last_read_ordinal,
        (
            CASE WHEN :completed = TRUE THEN e.last_chapter_id
                 ELSE NULL 
            END
        ) as last_read_chapter_id,
        (
            CASE WHEN :completed = TRUE THEN CURRENT_TIMESTAMP
                 ELSE NULL 
            END
        ) as last_read_at,
        CURRENT_TIMESTAMP as updated_at,
        CURRENT_TIMESTAMP as created_at
    FROM mb_manga m
    JOIN mb_manga_ext e ON e.manga_id = m.id
    JOIN mb_profiles p ON p.id = :profileId
    WHERE 
        m.id = :mangaId AND
        m.deleted_at IS NULL AND
        p.deleted_at IS NULL AND
        e.deleted_at IS NULL
    ON CONFLICT (profile_id, manga_id) 
    DO UPDATE SET
        is_completed = EXCLUDED.is_completed,
        progress_percentage = EXCLUDED.progress_percentage,
        last_read_ordinal = EXCLUDED.last_read_ordinal,
        last_read_chapter_id = EXCLUDED.last_read_chapter_id,
        last_read_at = EXCLUDED.last_read_at,
        updated_at = EXCLUDED.updated_at
    RETURNING id, manga_id
)
INSERT INTO mb_chapter_progress
(
    progress_id,
    chapter_id,
    page_ordinal,
    last_read,
    bookmarks,
    updated_at,
    created_at
)
SELECT
    m.id as progress_id,
    c.id as chapter_id,
    (
        CASE WHEN :completed = TRUE THEN COALESCE(c.page_count, 1)
             ELSE NULL
        END
    ) as page_ordinal,
    (
        CASE WHEN :completed = TRUE THEN COALESCE(cp.last_read, CURRENT_TIMESTAMP)
             ELSE NULL
        END
    ) as last_read,
    '{}' as bookmarks,
    CURRENT_TIMESTAMP as updated_at,
    CURRENT_TIMESTAMP as created_at
FROM manga_upserts m
JOIN mb_chapters c ON c.manga_id = m.manga_id
LEFT JOIN mb_chapter_progress cp ON cp.progress_id = m.id AND cp.chapter_id = c.id
WHERE 
    c.deleted_at IS NULL AND
    cp.deleted_at IS NULL
ON CONFLICT (progress_id, chapter_id) DO UPDATE SET
    page_ordinal = EXCLUDED.page_ordinal,
    last_read = EXCLUDED.last_read,
    updated_at = EXCLUDED.updated_at;
