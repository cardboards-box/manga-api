INSERT INTO mb_manga_progress
(
    profile_id,
    manga_id,
    favorited,
    is_completed,
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
        CASE WHEN :completed THEN e.last_chapter_ordinal
             ELSE NULL 
        END
    ) as last_read_ordinal,
    (
        CASE WHEN :completed THEN e.last_chapter_id
             ELSE NULL 
        END
    ) as last_read_chapter_id,
    (
        CASE WHEN :completed THEN CURRENT_TIMESTAMP
             ELSE NULL 
        END
    ) as last_read_at,
    CURRENT_TIMESTAMP as updated_at,
    CURRENT_TIMESTAMP as created_at
FROM mb_manga m
JOIN mb_manga_ext e ON e.manga_id = m.id
JOIN mb_profiles p ON p.id = :profileId
WHERE 
    m.id = ANY(:ids) AND
    m.deleted_at IS NULL AND
    p.deleted_at IS NULL AND
    e.deleted_at IS NULL
ON CONFLICT (profile_id, manga_id) 
DO UPDATE SET
    is_completed = EXCLUDED.is_completed,
    last_read_ordinal = EXCLUDED.last_read_ordinal,
    last_read_chapter_id = EXCLUDED.last_read_chapter_id,
    last_read_at = EXCLUDED.last_read_at,
    updated_at = EXCLUDED.updated_at;