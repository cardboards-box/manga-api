WITH input AS (
    SELECT '8c1fe40b-93d5-40c5-ac36-1199c4aa5d35' as id
), paras AS (
    SELECT
        try_cast(p.id, NULL::uuid) as id_uuid,
        try_cast(p.id, 0) as id_integer,
        p.id as id
    FROM input p
), entities AS (
    SELECT
        m.id,
        'manga' as type,
        m.deleted_at
    FROM mb_manga m
    JOIN paras p ON
        p.id_uuid = m.id OR
        p.id = m.original_source_id OR
        p.id_integer = m.legacy_id OR
        p.id = m.url

    UNION ALL

    SELECT
        c.id,
        'chapter',
        c.deleted_at
    FROM mb_chapters c
    JOIN paras p ON
        p.id_uuid = c.id OR
        p.id = c.source_id OR
        p.id = c.url

    UNION ALL

    SELECT
        s.id,
        'source',
        s.deleted_at
    FROM mb_sources s
    JOIN paras p ON
        p.id_uuid = s.id OR
        p.id = s.name OR
        p.id = s.slug

    UNION ALL

    SELECT
        i.id,
        'image',
        i.deleted_at
    FROM mb_images i
    JOIN paras p ON
        p.id_uuid = i.id OR
        p.id = i.url OR
        p.id = i.url_hash

    UNION ALL

    SELECT
        r.id,
        'people',
        r.deleted_at
    FROM mb_people r
    JOIN paras p ON
        p.id_uuid = r.id OR
        p.id = r.name

    UNION ALL

    SELECT
        r.id,
        'profile',
        r.deleted_at
    FROM mb_profiles r
    JOIN paras p ON
        p.id_uuid = r.id OR
        p.id = r.platform_id
)
SELECT * FROM entities;