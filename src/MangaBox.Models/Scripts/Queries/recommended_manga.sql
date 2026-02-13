BEGIN;
DROP TABLE IF EXISTS temp_related_manga_{0};

CREATE TEMP TABLE temp_related_manga_{0} ON COMMIT DROP AS
WITH seed AS (
    SELECT
        m.id,
        m.source_id,
        m.content_rating,
        m.nsfw,
        m.title
    FROM mb_manga m
    WHERE m.id = :mangaId
      AND m.deleted_at IS NULL
), seed_tags AS (
    SELECT mt.tag_id
    FROM mb_manga_tags mt
    JOIN seed s ON s.id = mt.manga_id
), seed_tag_count AS (
    SELECT COUNT(*)::numeric AS cnt
    FROM seed_tags
), seed_people AS (
    SELECT mr.person_id, mr.type
    FROM mb_manga_relationships mr
    JOIN seed s ON s.id = mr.manga_id
), seed_people_count AS (
    SELECT COUNT(*)::numeric AS cnt
    FROM seed_people
), candidate_base AS (
    SELECT
        m.id,
        m.source_id,
        m.content_rating,
        m.nsfw,
        m.title
    FROM mb_manga m
    JOIN seed s ON TRUE
    WHERE m.id <> s.id
      AND m.deleted_at IS NULL
      AND NOT m.is_hidden
), cand_tag_counts AS (
    SELECT mt.manga_id, COUNT(*)::numeric AS tag_cnt
    FROM mb_manga_tags mt
    JOIN candidate_base c ON c.id = mt.manga_id
    GROUP BY mt.manga_id
), tag_intersections AS (
    SELECT
        mt.manga_id,
        COUNT(*)::numeric AS inter_cnt
    FROM mb_manga_tags mt
    JOIN seed_tags st ON st.tag_id = mt.tag_id
    JOIN candidate_base c ON c.id = mt.manga_id
    GROUP BY mt.manga_id
), people_overlap AS (
    SELECT
        c.id AS manga_id,
        SUM(
            CASE sp.type
                WHEN 0 THEN 3  -- Author
                WHEN 1 THEN 2  -- Artist
                WHEN 2 THEN 1  -- Uploader
                ELSE 1
            END
        )::numeric AS people_score_raw,
        COUNT(*)::numeric AS people_match_cnt
    FROM candidate_base c
    JOIN mb_manga_relationships mr ON mr.manga_id = c.id
    JOIN seed_people sp
      ON sp.person_id = mr.person_id
     AND sp.type      = mr.type
    GROUP BY c.id
)
SELECT
    c.id AS id,
    COALESCE(ti.inter_cnt, 0) AS tag_intersection,
    COALESCE(ct.tag_cnt, 0) AS candidate_tag_count,
    stc.cnt AS seed_tag_count,
    COALESCE(po.people_match_cnt, 0) AS people_match_count,
    COALESCE(po.people_score_raw, 0) AS people_score_raw,
    CASE
        WHEN (stc.cnt + COALESCE(ct.tag_cnt, 0) - COALESCE(ti.inter_cnt, 0)) <= 0 THEN 0
        ELSE COALESCE(ti.inter_cnt, 0)
             / (stc.cnt + COALESCE(ct.tag_cnt, 0) - COALESCE(ti.inter_cnt, 0))
    END AS tag_jaccard,
    CASE
        WHEN spc.cnt <= 0 THEN 0
        ELSE COALESCE(po.people_score_raw, 0) / (spc.cnt * 3)
    END AS people_norm,
    (1 - (ABS(c.content_rating - s.content_rating)::numeric / 3)) AS rating_norm,
    (CASE WHEN c.source_id = s.source_id THEN 1 ELSE 0 END)::numeric AS same_source_norm,
    similarity(c.title, s.title)::numeric AS title_sim,
    (
        0.62 * (CASE
            WHEN (stc.cnt + COALESCE(ct.tag_cnt, 0) - COALESCE(ti.inter_cnt, 0)) <= 0 THEN 0
            ELSE COALESCE(ti.inter_cnt, 0) / (stc.cnt + COALESCE(ct.tag_cnt, 0) - COALESCE(ti.inter_cnt, 0))
        END) + 0.15 * similarity(c.title, s.title)::numeric
        + 0.13 * (CASE
            WHEN spc.cnt <= 0 THEN 0
            ELSE COALESCE(po.people_score_raw, 0) / (spc.cnt * 3)
        END) + 0.07 * (1 - (ABS(c.content_rating - s.content_rating)::numeric / 3))
        + 0.03 * (CASE WHEN c.source_id = s.source_id THEN 1 ELSE 0 END)::numeric
    ) AS similarity_score
FROM candidate_base c
JOIN seed s ON TRUE
CROSS JOIN seed_tag_count stc
CROSS JOIN seed_people_count spc
LEFT JOIN cand_tag_counts ct ON ct.manga_id = c.id
LEFT JOIN tag_intersections ti ON ti.manga_id = c.id
LEFT JOIN people_overlap po ON po.manga_id = c.id
WHERE
    COALESCE(ti.inter_cnt, 0) > 0 OR
    COALESCE(po.people_match_cnt, 0) > 0 OR
    similarity(c.title, s.title) >= 0.20
ORDER BY
    similarity_score DESC,
    tag_intersection DESC,
    title_sim DESC,
    people_match_count DESC
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
    t.tag_intersection DESC,
    t.title_sim DESC,
    t.people_match_count DESC;

DROP TABLE IF EXISTS temp_related_manga_{0};
COMMIT;