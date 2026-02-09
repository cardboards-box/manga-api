WITH input AS (
	SELECT
		@profileId::uuid AS profile_id,
		@chapterId::uuid AS chapter_id,
		@pageOrdinal::integer AS page_ordinal
), progresses AS (
	INSERT INTO mb_manga_progress (
		profile_id,
		manga_id,
		favorited,
		is_completed,
		progress_percentage,
		last_read_at,
		last_read_ordinal,
		last_read_chapter_id,
		updated_at,
		created_at
	)
	SELECT
		p.id as profile_id,
		c.manga_id,
		false as favorited,
		(
			CASE WHEN m.id IS NULL THEN FALSE
				 WHEN m.last_chapter_ordinal <= c.ordinal THEN TRUE
				 ELSE FALSE
			END
		) as is_completed,
		(
			CASE WHEN m.id IS NULL THEN 0
				 WHEN f.manga_id IS NULL THEN 0
				 WHEN a.manga_id IS NULL THEN 0
				 ELSE CAST(a.number as NUMERIC) / f.number * 100
			END
		) as progress_percentage,
		CURRENT_TIMESTAMP as last_read_at,
		c.ordinal as last_read_ordinal,
		c.id as last_read_chapter_id,
		CURRENT_TIMESTAMP as updated_at,
		CURRENT_TIMESTAMP as created_at
	FROM input i
	JOIN mb_chapters c ON c.id = i.chapter_id
	JOIN mb_profiles p ON p.id = i.profile_id
	LEFT JOIN mb_manga_ext m ON m.manga_id = c.manga_id
	LEFT JOIN mb_vw_chapter_numbers f ON
        f.manga_id = m.manga_id AND
        f.ordinal = m.last_chapter_ordinal
    LEFT JOIN mb_vw_chapter_numbers a ON
        a.manga_id = m.manga_id AND
        a.ordinal = c.ordinal
	ON CONFLICT (profile_id, manga_id) DO UPDATE SET
		is_completed = EXCLUDED.is_completed,
		progress_percentage = EXCLUDED.progress_percentage,
		last_read_at = CURRENT_TIMESTAMP,
	    last_read_ordinal = EXCLUDED.last_read_ordinal,
	    last_read_chapter_id = EXCLUDED.last_read_chapter_id,
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
	i.page_ordinal,
	CURRENT_TIMESTAMP,
	'{}',
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
FROM input i
JOIN progresses p ON p.profile_id = i.profile_id
ON CONFLICT (progress_id, chapter_id) DO UPDATE SET
	page_ordinal = EXCLUDED.page_ordinal,
	last_read = CURRENT_TIMESTAMP,
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