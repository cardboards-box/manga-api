WITH earliest AS (
    SELECT
        MIN(m.updated_at) as updated_at,
        m.source_id
    FROM mb_manga m
    GROUP BY m.source_id
), refresh_manga AS (
    SELECT
        m.*
    FROM mb_manga m
    JOIN earliest e ON
        e.source_id = m.source_id AND
        e.updated_at = m.updated_at
    WHERE
        m.updated_at < CURRENT_TIMESTAMP + INTERVAL '-6 hours'
), do_update AS (
    UPDATE mb_manga
    SET updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        SELECT id
        FROM refresh_manga
    )
)
SELECT *
FROM refresh_manga;