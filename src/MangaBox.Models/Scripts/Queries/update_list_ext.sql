WITH target_lists AS (
	SELECT l.id
	FROM mb_lists l
	WHERE 
		l.deleted_at IS NULL AND (
			COALESCE(cardinality(@ids::uuid[]), 0) = 0 OR
			l.id = ANY(@ids::uuid[])
		)
), manga_counts AS (
	SELECT
		tl.id AS list_id,
		COUNT(li.manga_id)::int AS manga_count
	FROM target_lists tl
	LEFT JOIN mb_list_items li ON 
		li.list_id = tl.id AND 
		li.deleted_at IS NULL
	LEFT JOIN mb_manga m ON 
		m.id = li.manga_id AND 
		m.deleted_at IS NULL
	GROUP BY tl.id
), clone_counts AS (
	SELECT
		tl.id AS list_id,
		COUNT(DISTINCT c.clone_id)::int AS cloned_count
	FROM target_lists tl
	LEFT JOIN LATERAL (
		SELECT c1.id AS clone_id
		FROM mb_lists c1
		WHERE 
			c1.deleted_at IS NULL AND
			c1.cloned_from = tl.id
		UNION
		SELECT c2.id AS clone_id
		FROM mb_lists c1
		JOIN mb_lists c2 ON 
			c2.cloned_from = c1.id AND
			c2.deleted_at IS NULL
		WHERE 
			c1.deleted_at IS NULL AND
			c1.cloned_from = tl.id
	) c ON TRUE
	GROUP BY tl.id
), cover_candidates AS (
	SELECT
		tl.id AS list_id,
		cc.cover_id
	FROM target_lists tl
	LEFT JOIN LATERAL (
		SELECT p.id AS cover_id
		FROM mb_list_items li
		JOIN mb_manga m ON 
			m.id = li.manga_id AND
			m.deleted_at IS NULL
		JOIN mb_images p ON 
			p.manga_id = m.id AND
			p.chapter_id IS NULL AND
			p.deleted_at IS NULL AND
			p.last_failed_at IS NULL
		WHERE 
			li.list_id = tl.id AND
			li.deleted_at IS NULL
		ORDER BY
			li.created_at ASC,
			p.ordinal DESC,
			p.created_at ASC,
			p.id ASC
		LIMIT 1
	) cc ON TRUE
), upsert_source AS (
	SELECT
		tl.id AS list_id,
		cc.cover_id,
		COALESCE(mc.manga_count, 0) AS manga_count,
		COALESCE(clc.cloned_count, 0) AS cloned_count
	FROM target_lists tl
	LEFT JOIN manga_counts mc ON mc.list_id = tl.id
	LEFT JOIN clone_counts clc ON clc.list_id = tl.id
	LEFT JOIN cover_candidates cc ON cc.list_id = tl.id
)
INSERT INTO mb_list_ext AS ext (
	list_id,
	cover_id,
	manga_count,
	cloned_count,
	created_at,
	updated_at
)
SELECT
	s.list_id,
	s.cover_id,
	s.manga_count,
	s.cloned_count,
	CURRENT_TIMESTAMP,
	CURRENT_TIMESTAMP
FROM upsert_source s
ON CONFLICT (list_id) DO UPDATE
SET
	cover_id = CASE
		WHEN ext.cover_id IS NULL AND
			 EXCLUDED.cover_id IS NOT NULL
		THEN EXCLUDED.cover_id
		ELSE ext.cover_id
	END,
	manga_count = EXCLUDED.manga_count,
	cloned_count = EXCLUDED.cloned_count,
	updated_at = CURRENT_TIMESTAMP
RETURNING
	ext.id,
	ext.list_id,
	ext.cover_id,
	ext.manga_count,
	ext.cloned_count,
	ext.created_at,
	ext.updated_at,
	ext.deleted_at;