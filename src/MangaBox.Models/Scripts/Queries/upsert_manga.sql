WITH input AS (
    SELECT
        @source_id::uuid AS source_id,
        @manga_json::jsonb AS j,
        @profile_id::uuid AS profile_id
), manga_in AS (
    SELECT
        NULLIF(j->>'title','') AS title,
        COALESCE(ARRAY(SELECT jsonb_array_elements_text(j->'altTitles')), '{}'::text[]) AS alt_titles,
        NULLIF(j->>'description','') AS description,
        COALESCE(ARRAY(SELECT jsonb_array_elements_text(j->'altDescriptions')), '{}'::text[]) AS alt_descriptions,
        NULLIF(j->>'homePage','') AS url,
        NULLIF(j->>'id','') AS original_source_id,
        COALESCE((j->>'nsfw')::boolean, false) AS nsfw,
        COALESCE((j->>'rating')::int, 0) AS content_rating,
        NULLIF(j->>'referer','') AS referer,
        CASE
            WHEN (j ? 'sourceCreated') AND (j->>'sourceCreated') <> '' THEN (j->>'sourceCreated')::timestamp
            ELSE NULL
        END AS source_created,
        COALESCE((j->>'ordinalVolumeReset')::boolean, false) AS ordinal_volume_reset,
        NULLIF(j->>'cover','') AS cover_url,
        j->'attributes' AS attributes_json,
        j->'chapters'   AS chapters_json,
        COALESCE(ARRAY(SELECT jsonb_array_elements_text(j->'authors')), '{}'::text[])   AS author_names,
        COALESCE(ARRAY(SELECT jsonb_array_elements_text(j->'artists')), '{}'::text[])   AS artist_names,
        COALESCE(ARRAY(SELECT jsonb_array_elements_text(j->'uploaders')), '{}'::text[]) AS uploader_names,
        (j->>'legacyId')::int AS legacy_id
    FROM input
), manga_attrs AS (
    SELECT
        COALESCE(ARRAY(
            SELECT (a.name, a.value)::mb_attribute
            FROM jsonb_to_recordset(COALESCE((SELECT attributes_json FROM manga_in), '[]'::jsonb))
                AS a(name text, value text)
        ), '{}'::mb_attribute[]) AS attrs
), upsert_manga AS (
    INSERT INTO mb_manga (
        title,
        alt_titles,
        description,
        alt_descriptions,
        url,
        attributes,
        content_rating,
        nsfw,
        source_id,
        original_source_id,
        is_hidden,
        referer,
        user_agent,
        source_created,
        ordinal_volume_reset,
        updated_at,
        legacy_id
    )
    SELECT
        mi.title,
        mi.alt_titles,
        mi.description,
        mi.alt_descriptions,
        mi.url,
        ma.attrs,
        mi.content_rating,
        mi.nsfw,
        i.source_id,
        mi.original_source_id,
        false,
        mi.referer,
        null,
        mi.source_created,
        mi.ordinal_volume_reset,
        CURRENT_TIMESTAMP,
        mi.legacy_id
    FROM manga_in mi
    CROSS JOIN manga_attrs ma
    CROSS JOIN input i
    ON CONFLICT (source_id, original_source_id)
    DO UPDATE SET
        title = EXCLUDED.title,
        alt_titles = EXCLUDED.alt_titles,
        description = EXCLUDED.description,
        alt_descriptions = EXCLUDED.alt_descriptions,
        url = EXCLUDED.url,
        attributes = EXCLUDED.attributes,
        content_rating = EXCLUDED.content_rating,
        nsfw = EXCLUDED.nsfw,
        legacy_id = COALESCE(EXCLUDED.legacy_id, mb_manga.legacy_id),
        updated_at = CURRENT_TIMESTAMP
    RETURNING mb_manga.*, (xmax = 0) AS is_new
), chapters_in AS (
    SELECT
        um.id AS manga_id,
        c.*
    FROM upsert_manga um
    JOIN manga_in mi ON TRUE
    CROSS JOIN LATERAL jsonb_to_recordset(COALESCE(mi.chapters_json, '[]'::jsonb)) AS c(
        title text,
        url text,
        id text,
        number numeric,
        volume numeric,
        language text,
        "externalUrl" text,
        attributes jsonb,
        "legacyId" integer,
        pages jsonb
    )
), chapters_upsert AS (
    INSERT INTO mb_chapters (
        manga_id,
        title,
        url,
        source_id,
        ordinal,
        volume,
        language,
        external_url,
        attributes,
        page_count,
        updated_at,
        legacy_id
    )
    SELECT
        ci.manga_id,
        NULLIF(ci.title,'') AS title,
        NULLIF(ci.url,'') AS url,
        ci.id AS source_id,
        ci.number AS ordinal,
        ci.volume AS volume,
        COALESCE(NULLIF(ci.language,''), 'en') AS language,
        NULLIF(ci."externalUrl",'') AS external_url,
        COALESCE(ARRAY(
            SELECT (a.name, a.value)::mb_attribute
            FROM jsonb_to_recordset(COALESCE(ci.attributes, '[]'::jsonb))
                    AS a(name text, value text)
        ),'{}'::mb_attribute[]) AS attributes,
        CASE
            WHEN ci.pages IS NULL THEN NULL
            ELSE jsonb_array_length(COALESCE(ci.pages, '[]'::jsonb))
        END AS page_count,
        CURRENT_TIMESTAMP as updated_at,
        ci."legacyId" as legacy_id
    FROM chapters_in ci
    ON CONFLICT (manga_id, source_id)
    DO UPDATE SET
        title = EXCLUDED.title,
        url = EXCLUDED.url,
        ordinal = EXCLUDED.ordinal,
        volume = EXCLUDED.volume,
        language = EXCLUDED.language,
        external_url = EXCLUDED.external_url,
        attributes = EXCLUDED.attributes,
        page_count = (
            CASE WHEN EXCLUDED.page_count IS NULL THEN mb_chapters.page_count
                 WHEN EXCLUDED.page_count = 0 THEN mb_chapters.page_count
                 ELSE EXCLUDED.page_count
            END
        ),
        updated_at = CURRENT_TIMESTAMP,
        legacy_id = COALESCE(EXCLUDED.legacy_id, mb_chapters.legacy_id)
    RETURNING mb_chapters.*, (xmax = 0) AS is_new
), pages_in AS (
    SELECT
        cu.id       AS chapter_id,
        cu.manga_id AS manga_id,
        ci.pages    AS pages
    FROM chapters_upsert cu
    JOIN chapters_in ci
        ON ci.manga_id = cu.manga_id
       AND ci.id = cu.source_id
    WHERE ci.pages IS NOT NULL
), pages_upsert AS (
    INSERT INTO mb_images (
        url,
        chapter_id,
        manga_id,
        ordinal,
        image_width,
        image_height,
        updated_at
    )
    SELECT
        NULLIF(p.elem->>'page','') AS url,
        pi.chapter_id,
        pi.manga_id,
        (p.ord)::int AS ordinal,
        NULLIF(p.elem->>'width','')::int  AS image_width,
        NULLIF(p.elem->>'height','')::int AS image_height,
        CURRENT_TIMESTAMP
    FROM pages_in pi
    CROSS JOIN LATERAL jsonb_array_elements(COALESCE(pi.pages, '[]'::jsonb)) WITH ORDINALITY AS p(elem, ord)
    WHERE NULLIF(p.elem->>'page','') IS NOT NULL
    ON CONFLICT (chapter_id, manga_id, ordinal)
    DO UPDATE SET
        url = EXCLUDED.url,
        image_width = EXCLUDED.image_width,
        image_height = EXCLUDED.image_height,
        updated_at = CURRENT_TIMESTAMP,
        deleted_at = NULL
    RETURNING id
), chapters_page_updates AS (
    UPDATE mb_chapters c
    SET
        updated_at = CURRENT_TIMESTAMP,
        page_count = (
            SELECT COUNT(*)
            FROM mb_images i
            WHERE 
                i.chapter_id = c.id AND 
                i.deleted_at IS NULL
        )
    WHERE c.manga_id IN (
        SELECT id FROM upsert_manga
    )
), cover_existing AS (
    SELECT
        img.id,
        img.ordinal
    FROM mb_images img
    JOIN upsert_manga um ON um.id = img.manga_id
    JOIN manga_in mi ON TRUE
    WHERE
        img.chapter_id IS NULL AND
        img.manga_id = um.id AND
        img.url = mi.cover_url AND
        img.deleted_at IS NULL
    LIMIT 1
), cover_insert AS (
    INSERT INTO mb_images (
        url,
        chapter_id,
        manga_id,
        ordinal,
        updated_at
    )
    SELECT
        mi.cover_url,
        NULL::uuid,
        um.id,
        COALESCE((
            SELECT MAX(i2.ordinal) + 1
            FROM mb_images i2
            WHERE i2.manga_id = um.id
                AND i2.chapter_id IS NULL
                AND i2.deleted_at IS NULL
        ), 1) AS next_ordinal,
        CURRENT_TIMESTAMP
    FROM manga_in mi
    JOIN upsert_manga um ON TRUE
    WHERE
        mi.cover_url IS NOT NULL AND
        NOT EXISTS (SELECT 1 FROM cover_existing)
    ON CONFLICT ON CONSTRAINT mb_images_unique
    DO NOTHING
    RETURNING id, ordinal
), profile_person AS (
    SELECT
        p.username AS name,
        p.avatar   AS avatar,
        p.id       AS profile_id
    FROM mb_profiles p
    JOIN input i ON i.profile_id = p.id
    WHERE
        p.deleted_at IS NULL AND
        i.profile_id IS NOT NULL
), people_in AS (
    SELECT DISTINCT
        NULLIF(btrim(p.name), '') AS name,
        p.rel_type::int AS rel_type,
        NULL::uuid      AS profile_id,
        NULL::text      AS avatar
    FROM manga_in mi
    CROSS JOIN LATERAL (
        SELECT unnest(mi.author_names) AS name, 0 AS rel_type
        UNION ALL
        SELECT unnest(mi.artist_names) AS name, 1 AS rel_type
        UNION ALL
        SELECT unnest(mi.uploader_names) AS name, 2 AS rel_type
    ) p
    WHERE NULLIF(btrim(p.name), '') IS NOT NULL

    UNION ALL

    SELECT
        NULLIF(btrim(pp.name), '') AS name,
        2 AS rel_type,
        pp.profile_id,
        pp.avatar
    FROM profile_person pp
    WHERE NULLIF(btrim(pp.name), '') IS NOT NULL

), people_rollup AS (
    SELECT
        name,
        bool_or(rel_type = 0) AS is_author,
        bool_or(rel_type = 1) AS is_artist,
        bool_or(rel_type = 2) AS is_user,
        (ARRAY_AGG(profile_id) FILTER (WHERE profile_id IS NOT NULL))[1] AS profile_id,
        (ARRAY_AGG(avatar) FILTER (WHERE avatar IS NOT NULL))[1] AS avatar
    FROM people_in
    GROUP BY name
), people_upsert AS (
    INSERT INTO mb_people (
        name,
        avatar,
        artist,
        author,
        is_user,
        profile_id,
        updated_at
    )
    SELECT
        pr.name,
        pr.avatar,
        pr.is_artist,
        pr.is_author,
        pr.is_user,
        pr.profile_id,
        CURRENT_TIMESTAMP
    FROM people_rollup pr
    ON CONFLICT (name)
    DO UPDATE SET
        artist = mb_people.artist OR EXCLUDED.artist,
        author = mb_people.author OR EXCLUDED.author,
        is_user = mb_people.is_user OR EXCLUDED.is_user,
        avatar = COALESCE(mb_people.avatar, EXCLUDED.avatar),
        profile_id = COALESCE(mb_people.profile_id, EXCLUDED.profile_id),
        updated_at = CURRENT_TIMESTAMP
    RETURNING id, name
), relationships_in AS (
    SELECT
        DISTINCT
        um.id AS manga_id,
        pu.id AS person_id,
        pi.rel_type AS type
    FROM upsert_manga um
    JOIN people_in pi ON TRUE
    JOIN people_upsert pu ON pu.name = pi.name
), relationships_upsert AS (
    INSERT INTO mb_manga_relationships (
        manga_id,
        person_id,
        type,
        updated_at
    )
    SELECT
        ri.manga_id,
        ri.person_id,
        ri.type,
        CURRENT_TIMESTAMP
    FROM relationships_in ri
    ON CONFLICT (manga_id, person_id, type)
    DO UPDATE SET
        updated_at = CURRENT_TIMESTAMP
    RETURNING id
), tags_in AS (
    SELECT DISTINCT
        NULLIF(t.name, '') AS name,
        NULLIF(t.slug, '') AS slug
    FROM input i
    CROSS JOIN LATERAL jsonb_to_recordset(COALESCE(i.j->'tags', '[]'::jsonb))
        AS t(name text, slug text)
    WHERE NULLIF(t.slug, '') IS NOT NULL
),
tags_upsert AS (
    INSERT INTO mb_tags (
        name,
        slug,
        description,
        source_id,
        updated_at
    )
    SELECT
        ti.name,
        ti.slug,
        NULL,
        i.source_id,
        CURRENT_TIMESTAMP
    FROM tags_in ti
    CROSS JOIN input i
    ON CONFLICT (slug)
    DO UPDATE SET
        name = COALESCE(EXCLUDED.name, mb_tags.name),
        updated_at = CURRENT_TIMESTAMP
    RETURNING id, slug
),
manga_tags_upsert AS (
    INSERT INTO mb_manga_tags (
        manga_id,
        tag_id,
        updated_at
    )
    SELECT DISTINCT
        um.id,
        tu.id,
        CURRENT_TIMESTAMP
    FROM upsert_manga um
    JOIN tags_upsert tu ON TRUE
    ON CONFLICT ON CONSTRAINT mb_manga_tags_unique
    DO UPDATE SET
        updated_at = CURRENT_TIMESTAMP
    RETURNING id
), manga_json AS (
    SELECT jsonb_build_object(
        'legacyId', um.legacy_id,
        'id', um.id,
        'createdAt', um.created_at,
        'updatedAt', um.updated_at,
        'deletedAt', um.deleted_at,
        'title', um.title,
        'altTitles', um.alt_titles,
        'description', um.description,
        'altDescriptions', um.alt_descriptions,
        'url', um.url,
        'attributes', COALESCE(
            (
                SELECT jsonb_agg(jsonb_build_object('name', a.name, 'value', a.value))
                FROM unnest(um.attributes) AS a
            ),
            '[]'::jsonb
        ),
        'contentRating', um.content_rating,
        'nsfw', um.nsfw,
        'sourceId', um.source_id,
        'originalSourceId', um.original_source_id,
        'isHidden', um.is_hidden,
        'referer', um.referer,
        'userAgent', um.user_agent,
        'sourceCreated', um.source_created,
        'ordinalVolumeReset', um.ordinal_volume_reset
    ) AS j,
    um.is_new AS is_new
    FROM upsert_manga um
), chapters_json AS (
    SELECT
        cu.is_new,
        jsonb_build_object(
            'legacyId', c.legacy_id,
            'id', c.id,
            'createdAt', c.created_at,
            'updatedAt', c.updated_at,
            'deletedAt', c.deleted_at,
            'mangaId', c.manga_id,
            'title', c.title,
            'url', c.url,
            'sourceId', c.source_id,
            'ordinal', c.ordinal,
            'volume', c.volume,
            'language', c.language,
            'externalUrl', c.external_url,
            'pageCount', c.page_count,
            'attributes', COALESCE(
                (
                    SELECT jsonb_agg(jsonb_build_object('name', a.name, 'value', a.value))
                    FROM unnest(c.attributes) AS a
                ),
                '[]'::jsonb
            )
        ) AS j
    FROM chapters_upsert cu
    JOIN mb_chapters c ON c.id = cu.id
)
SELECT jsonb_build_object(
    'manga', (SELECT j FROM manga_json),
    'chaptersUpdated', COALESCE(
        (SELECT jsonb_agg(j ORDER BY (j->>'sourceId'))
         FROM chapters_json
         WHERE is_new = false),
        '[]'::jsonb
    ),
    'chaptersNew', COALESCE(
        (SELECT jsonb_agg(j ORDER BY (j->>'sourceId'))
         FROM chapters_json
         WHERE is_new = true),
        '[]'::jsonb
    ),
    'mangaIsNew', (SELECT is_new FROM manga_json)
) AS result;
