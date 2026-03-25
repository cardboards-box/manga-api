WITH log_intervals AS (
    SELECT 2 as log_level, CURRENT_TIMESTAMP - interval '3 days' as date
    UNION ALL
    SELECT 3, CURRENT_TIMESTAMP - interval '7 days'
    UNION ALL
    SELECT 4, CURRENT_TIMESTAMP - interval '14 days'
), logs_to_delete AS (
    SELECT l.id
    FROM mb_logs l
    JOIN log_intervals i ON
        l.log_level = i.log_level AND
        l.created_at <= i.date
), do_delete AS (
    DELETE FROM mb_logs
    WHERE id IN (
        SELECT id 
        FROM logs_to_delete
    )
)
SELECT
    COUNT(*) as log_count,
    MIN(created_at) as min_created_at,
    MAX(created_at) as max_created_at,
    l.log_level,
    l.category
FROM mb_logs l
GROUP BY l.log_level, l.category
ORDER BY l.log_level, l.category;