WITH input AS (
	SELECT
		@profileId::uuid AS profile_id,
		@chapterId::uuid AS chapter_id,
		@bookmarks::integer[] AS bookmarks
), progresses AS (
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
		c.manga_id,
		false as favorited,
		false as is_completed,
		CURRENT_TIMESTAMP as updated_at,
		CURRENT_TIMESTAMP as created_at
	FROM input i
	JOIN mb_chapters c ON c.id = i.chapter_id
	JOIN mb_profiles p ON p.id = i.profile_id
	ON CONFLICT (profile_id, manga_id) DO UPDATE SET
	    updated_at = CURRENT_TIMESTAMP
	RETURNING *
)
INSERT INTO mb_chapter_progress (
	progress_id,
	chapter_id,
	page_ordinal,
	last_read,
	bookmarks,
	updated_at,
	created_at
)
SELECT
	p.id,
	i.chapter_id,
	NULL,
	NULL,
	i.bookmarks,
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
FROM input i
JOIN progresses p ON p.profile_id = i.profile_id
ON CONFLICT (progress_id, chapter_id) DO UPDATE SET
	bookmarks = EXCLUDED.bookmarks,
	updated_at = CURRENT_TIMESTAMP;

SELECT DISTINCT p.*
FROM mb_manga_progress p
JOIN mb_chapter_progress c ON c.progress_id = p.id
WHERE 
	p.profile_id = @profileId AND 
	c.chapter_id = @chapterId AND 
	p.deleted_at IS NULL AND
	c.deleted_at IS NULL;

WITH progresses AS (
	SELECT DISTINCT p.id
	FROM mb_manga_progress p
	JOIN mb_chapter_progress c ON c.progress_id = p.id
	WHERE 
		p.profile_id = @profileId AND 
		c.chapter_id = @chapterId AND 
		p.deleted_at IS NULL AND
		c.deleted_at IS NULL
)
SELECT DISTINCT c.*
FROM mb_chapter_progress c
JOIN progresses p ON c.progress_id = p.id
WHERE c.deleted_at IS NULL;