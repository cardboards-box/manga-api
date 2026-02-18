BEGIN;
DROP TABLE IF EXISTS temp_related_manga_{0};

CREATE TEMP TABLE temp_related_manga_{0} ON COMMIT DROP AS
WITH progress_rows AS (
    SELECT
        p.id AS progress_id,
        p.manga_id,
        COALESCE(p.progress_percentage, 0)::numeric AS progress_pct,
        p.is_completed,
        p.favorited,
        p.last_read_at,
        p.last_read_chapter_id
    FROM mb_manga_progress p
    WHERE
        p.profile_id = :profileId AND
        p.deleted_at IS NULL
), chapter_activity AS (
    SELECT
        cp.progress_id,
        BOOL_OR(cp.last_read IS NOT NULL) AS has_last_read,
        BOOL_OR(cp.page_ordinal IS NOT NULL) AS has_page,
        BOOL_OR(COALESCE(array_length(cp.bookmarks, 1), 0) > 0) AS has_bookmarks
    FROM mb_chapter_progress cp
    WHERE cp.deleted_at IS NULL
    GROUP BY cp.progress_id
), interacted AS (
    SELECT
        pr.manga_id,
        (
            (pr.progress_pct > 0 AND pr.progress_pct < 100) OR
            pr.is_completed OR
            pr.favorited OR
            pr.last_read_at IS NOT NULL OR
            pr.last_read_chapter_id IS NOT NULL OR
            COALESCE(ca.has_last_read, false) OR
            COALESCE(ca.has_page, false) OR
            COALESCE(ca.has_bookmarks, false)
        ) AS has_interaction,
        (
            LEAST(1.00, GREATEST(0.0, pr.progress_pct / 100.0))
            + CASE WHEN pr.is_completed THEN 0.35 ELSE 0 END
            + CASE WHEN pr.favorited THEN 0.25 ELSE 0 END
            + CASE WHEN pr.last_read_at IS NOT NULL THEN 0.10 ELSE 0 END
            + CASE WHEN COALESCE(ca.has_bookmarks, false) THEN 0.10 ELSE 0 END
        )::numeric AS raw_w
    FROM progress_rows pr
    LEFT JOIN chapter_activity ca ON ca.progress_id = pr.progress_id
), seed_set AS MATERIALIZED (
    SELECT
        i.manga_id,
        LEAST(1.50, GREATEST(0.10, i.raw_w))::numeric AS w
    FROM interacted i
    WHERE i.has_interaction
    ORDER BY i.raw_w DESC
    LIMIT 75
), seed_manga AS MATERIALIZED (
    SELECT
        m.id,
        m.title,
        m.content_rating,
        m.nsfw,
        ss.w
    FROM seed_set ss
    JOIN mb_manga m ON m.id = ss.manga_id
    WHERE
        m.deleted_at IS NULL AND
        NOT m.is_hidden
), seed_tag_weights AS MATERIALIZED (
    SELECT mt.tag_id, SUM(sm.w)::numeric AS w
    FROM seed_manga sm
    JOIN mb_manga_tags mt ON mt.manga_id = sm.id
    GROUP BY mt.tag_id
), seed_tag_total AS MATERIALIZED (
    SELECT COALESCE(SUM(w), 0)::numeric AS sum_w FROM seed_tag_weights
), seed_people_weights AS MATERIALIZED (
    SELECT
        mr.person_id,
        mr.type,
        SUM(sm.w * (CASE mr.type
            WHEN 0 THEN 1.00  -- Author
            WHEN 1 THEN 0.70  -- Artist
            WHEN 2 THEN 0.35  -- Uploader
            ELSE 0.35
        END))::numeric AS w
    FROM seed_manga sm
    JOIN mb_manga_relationships mr ON mr.manga_id = sm.id
    GROUP BY mr.person_id, mr.type
), seed_people_total AS MATERIALIZED (
    SELECT COALESCE(SUM(w), 0)::numeric AS sum_w FROM seed_people_weights
), seed_stats AS MATERIALIZED (
    SELECT AVG(sm.content_rating)::numeric AS avg_rating
    FROM seed_manga sm
), cand_by_tags AS MATERIALIZED (
    SELECT DISTINCT mt.manga_id
    FROM mb_manga_tags mt
    JOIN seed_tag_weights stw ON stw.tag_id = mt.tag_id
), cand_by_people AS MATERIALIZED (
    SELECT DISTINCT mr.manga_id
    FROM mb_manga_relationships mr
    JOIN seed_people_weights spw ON spw.person_id = mr.person_id AND spw.type = mr.type
), cand_by_title AS MATERIALIZED (
    SELECT DISTINCT m.id AS manga_id
    FROM mb_manga m
    JOIN seed_manga sm ON sm.title IS NOT NULL AND sm.title <> ''
    WHERE
        m.deleted_at IS NULL AND
        NOT m.is_hidden AND
        m.title IS NOT NULL AND
        m.title <> '' AND
        m.title % sm.title
), candidate_ids AS MATERIALIZED (
    SELECT manga_id FROM cand_by_tags
    UNION
    SELECT manga_id FROM cand_by_people
    UNION
    SELECT manga_id FROM cand_by_title
), candidates AS MATERIALIZED (
    SELECT m.id, m.title, m.content_rating, m.nsfw
    FROM mb_manga m
    JOIN candidate_ids ci ON ci.manga_id = m.id
    WHERE
        m.deleted_at IS NULL AND 
        NOT m.is_hidden AND 
        NOT EXISTS (
            SELECT 1
            FROM mb_manga_progress p
            WHERE p.profile_id = :profileId
            AND p.manga_id = m.id
            AND p.deleted_at IS NULL
        ) AND (
            :tagExcludes IS NULL OR 
            cardinality(:tagExcludes) = 0 OR 
            NOT EXISTS (
                SELECT 1
                FROM mb_manga_tags x
                WHERE 
                    x.manga_id = m.id AND 
                    x.tag_id = ANY(:tagExcludes::uuid[])
            )
        )
), candidate_tag_score AS (
    SELECT
        c.id AS manga_id,
        COALESCE(SUM(stw.w), 0)::numeric AS tag_score
    FROM candidates c
    JOIN mb_manga_tags mt ON mt.manga_id = c.id
    JOIN seed_tag_weights stw ON stw.tag_id = mt.tag_id
    GROUP BY c.id
), candidate_people_score AS (
    SELECT
        c.id AS manga_id,
        COALESCE(SUM(spw.w), 0)::numeric AS people_score
    FROM candidates c
    JOIN mb_manga_relationships mr ON mr.manga_id = c.id
    JOIN seed_people_weights spw ON spw.person_id = mr.person_id AND spw.type = mr.type
    GROUP BY c.id
), candidate_title_score AS (
    SELECT
        c.id AS manga_id,
        MAX(similarity(c.title, sm.title))::numeric AS title_sim
    FROM candidates c
    JOIN cand_by_title t ON t.manga_id = c.id
    JOIN seed_manga sm ON
        sm.title IS NOT NULL AND
        sm.title <> '' AND
        c.title  IS NOT NULL AND
        c.title  <> '' AND c.title % sm.title
    GROUP BY c.id
)
SELECT
    c.id AS id,
    c.title,
    COALESCE(cts.tag_score, 0)    AS tag_score,
    COALESCE(cps.people_score, 0) AS people_score,
    COALESCE(ctt.title_sim, 0)    AS title_sim,
    (1 - (ABS(c.content_rating - ss.avg_rating)::numeric / 3)) AS rating_norm,
    CASE WHEN stt.sum_w <= 0 THEN 0 ELSE LEAST(1, COALESCE(cts.tag_score, 0) / stt.sum_w) END AS tag_norm,
    CASE WHEN spt.sum_w <= 0 THEN 0 ELSE LEAST(1, COALESCE(cps.people_score, 0) / spt.sum_w) END AS people_norm,
    (
        0.74 * CASE WHEN stt.sum_w <= 0 THEN 0 ELSE LEAST(1, COALESCE(cts.tag_score, 0) / stt.sum_w) END
      + 0.14 * COALESCE(ctt.title_sim, 0)
      + 0.08 * CASE WHEN spt.sum_w <= 0 THEN 0 ELSE LEAST(1, COALESCE(cps.people_score, 0) / spt.sum_w) END
      + 0.04 * (1 - (ABS(c.content_rating - ss.avg_rating)::numeric / 3))
    ) AS similarity_score
FROM candidates c
CROSS JOIN seed_tag_total stt
CROSS JOIN seed_people_total spt
CROSS JOIN seed_stats ss
LEFT JOIN candidate_tag_score cts ON cts.manga_id = c.id
LEFT JOIN candidate_people_score cps ON cps.manga_id = c.id
LEFT JOIN candidate_title_score ctt ON ctt.manga_id = c.id
ORDER BY similarity_score DESC, tag_score DESC, title_sim DESC
LIMIT :limit;

SELECT i.*
FROM mb_images i
JOIN temp_related_manga_{0} p ON p.id = i.manga_id
WHERE i.chapter_id IS NULL AND i.deleted_at IS NULL;

SELECT e.*
FROM mb_manga_ext e
JOIN temp_related_manga_{0} p ON p.id = e.manga_id
WHERE e.deleted_at IS NULL;

SELECT DISTINCT s.*
FROM mb_sources s
JOIN mb_manga m ON m.source_id = s.id
JOIN temp_related_manga_{0} p ON p.id = m.id
WHERE 
	m.deleted_at IS NULL AND
	s.deleted_at IS NULL;

SELECT DISTINCT t.*
FROM mb_tags t 
JOIN mb_manga_tags mt ON mt.tag_id = t.id
JOIN temp_related_manga_{0} p ON p.id = mt.manga_id
WHERE t.deleted_at IS NULL AND mt.deleted_at IS NULL;

SELECT DISTINCT mt.manga_id, mt.tag_id
FROM mb_manga_tags mt
JOIN temp_related_manga_{0} p ON p.id = mt.manga_id
WHERE mt.deleted_at IS NULL;

SELECT m.*
FROM mb_manga m
JOIN temp_related_manga_{0} t ON m.id = t.id
ORDER BY 
    t.similarity_score DESC, 
    t.tag_score DESC, 
    t.title_sim DESC;

DROP TABLE IF EXISTS temp_related_manga_{0};
COMMIT;