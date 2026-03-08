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
        CASE
            WHEN i.profile_id IS NULL THEN 1
            WHEN p.id IS NULL THEN 2
            WHEN l.id IS NULL THEN 3
            WHEN m.id IS NULL THEN 4
            WHEN l.profile_id <> i.profile_id THEN 5
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
), upserted AS (
    INSERT INTO mb_list_items (
        list_id,
        manga_id,
        created_at,
        updated_at,
        deleted_at
    )
    SELECT
        v.list_id,
        v.manga_id,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        NULL
    FROM validation v
    WHERE v.error IS NULL
    ON CONFLICT (list_id, manga_id)
    DO UPDATE SET
        deleted_at = NULL,
        updated_at = CURRENT_TIMESTAMP
    RETURNING id
)
SELECT
    v.error,
    u.id
FROM validation v
LEFT JOIN upserted u ON TRUE;