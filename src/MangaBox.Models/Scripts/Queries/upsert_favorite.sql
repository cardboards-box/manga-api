INSERT INTO mb_manga_progress (
	profile_id, 
	manga_id, 
	favorited, 
	is_completed,
	updated_at,
	created_at
)
SELECT
    p.id as profile_id,
    m.id as manga_id,
    :favorite as favorited,
    FALSE as is_completed,
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
ON CONFLICT (profile_id, manga_id) DO UPDATE SET
	favorited = EXCLUDED.favorited,
	updated_at = EXCLUDED.updated_at;