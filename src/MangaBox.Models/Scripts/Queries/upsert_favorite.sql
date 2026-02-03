INSERT INTO mb_manga_progress (
	profile_id, 
	manga_id, 
	favorited, 
	is_completed,
	updated_at,
	created_at
)
VALUES (
	:profileId,
	:mangaId,
	:favorite,
	false,
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
)
ON CONFLICT (profile_id, manga_id) DO UPDATE SET
	favorited = EXCLUDED.favorited,
	updated_at = EXCLUDED.updated_at
RETURNING *;