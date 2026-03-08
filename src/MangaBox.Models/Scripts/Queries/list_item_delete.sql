WITH input AS (
    SELECT
        @ListId::uuid    AS list_id,
        @MangaId::uuid   AS manga_id,
        @ProfileId::uuid AS profile_id
), validation AS (
    SELECT
        i.list_id,
        i.manga_id,
        i.profile_id,
        p.id AS profile_exists,
        l.id AS list_exists,
        m.id AS manga_exists,
        li.id AS list_item_id,
        CASE
            WHEN i.profile_id IS NULL THEN 1
            WHEN p.id IS NULL THEN 2
            WHEN l.id IS NULL THEN 3
            WHEN m.id IS NULL THEN 4
            WHEN l.profile_id <> i.profile_id THEN 5
            WHEN li.id IS NULL THEN 6
            ELSE NULL
        END AS error
    FROM input i
    LEFT JOIN mb_profiles p
        ON p.id = i.profile_id
       AND p.deleted_at IS NULL
    LEFT JOIN mb_lists l
        ON l.id = i.list_id
       AND l.deleted_at IS NULL
    LEFT JOIN mb_manga m
        ON m.id = i.manga_id
       AND m.deleted_at IS NULL
    LEFT JOIN mb_list_items li
        ON li.list_id = i.list_id
       AND li.manga_id = i.manga_id
), deleted AS (
    UPDATE mb_list_items li
    SET
        deleted_at = CURRENT_TIMESTAMP,
        updated_at = CURRENT_TIMESTAMP
    FROM validation v
    WHERE v.error IS NULL
      AND li.id = v.list_item_id
    RETURNING li.id
)
SELECT
    v.error,
    d.id
FROM validation v
LEFT JOIN deleted d ON TRUE;