WITH
tag_src AS (
  SELECT
    m.id AS manga_legacy_id,
    btrim(t) AS tag_name
  FROM manga_cache m
  LEFT JOIN LATERAL unnest(COALESCE(m.tags, '{}'::text[])) AS t ON TRUE
  WHERE m.deleted_at IS NULL
),
tags_json AS (
  SELECT
    manga_legacy_id,
    jsonb_agg(DISTINCT jsonb_build_object('name', tag_name)) AS tags
  FROM tag_src
  WHERE tag_name IS NOT NULL AND tag_name <> ''
  GROUP BY manga_legacy_id
),

chapters_json AS (
  SELECT
    c.manga_id AS manga_legacy_id,
    jsonb_agg(
      jsonb_build_object(
        'title', NULLIF(c.title, ''),
        'url', c.url,
        'id', c.source_id,
        'number', c.ordinal,
        'volume', c.volume,
        'externalUrl', NULLIF(c.external_url, ''),
        'attributes', COALESCE(
          (
            SELECT jsonb_agg(jsonb_build_object('name', a.name, 'value', a.value))
            FROM unnest(COALESCE(c.attributes, '{}'::manga_attribute[])) AS a
          ),
          '[]'::jsonb
        ),
        'legacyId', (c.id * -1)::int
      )
      ORDER BY c.volume NULLS FIRST, c.ordinal, c.id
    ) AS chapters
  FROM manga_chapter_cache c
  JOIN manga_cache m ON m.id = c.manga_id
  WHERE c.deleted_at IS NULL
    AND m.deleted_at IS NULL
  GROUP BY c.manga_id
)

SELECT
  (m.id * -1)::int AS legacy_id,
  jsonb_build_object(
    'title', m.title,
    'id', m.source_id,
    'provider', m.provider,
    'homePage', m.url,
    'cover', m.cover,
    'description', NULLIF(m.description, ''),
    'altDescriptions', '[]'::jsonb,
    'altTitles', to_jsonb(COALESCE(m.alt_titles, '{}'::text[])),

    -- not in legacy schema
    'authors', '[]'::jsonb,
    'artists', '[]'::jsonb,

    -- uploader username(s)
    'uploaders',
      CASE
        WHEN m.uploader IS NULL THEN '[]'::jsonb
        ELSE COALESCE(
          (
            SELECT jsonb_agg(p.username)
            FROM profiles p
            WHERE p.id = m.uploader
              AND p.deleted_at IS NULL
          ),
          '[]'::jsonb
        )
      END,

    -- legacy schema has nsfw but no explicit rating; map nsfw=>Erotica(2) else Safe(0)
    'rating', CASE WHEN m.nsfw THEN 2 ELSE 0 END,

    'chapters', COALESCE(cj.chapters, '[]'::jsonb),
    'nsfw', m.nsfw,

    'attributes', COALESCE(
      (
        SELECT jsonb_agg(jsonb_build_object('name', a.name, 'value', a.value))
        FROM unnest(COALESCE(m.attributes, '{}'::manga_attribute[])) AS a
      ),
      '[]'::jsonb
    ),

    -- tags (name only; slug computed in C#)
    'tags', COALESCE(tj.tags, '[]'::jsonb),

    'referer', m.referer,
    'sourceCreated', m.source_created,
    'ordinalVolumeReset', m.ordinal_volume_reset,
    'legacyId', (m.id * -1)::int
  ) AS manga_json,
  m.url as manga_url
FROM manga_cache m
LEFT JOIN chapters_json cj ON cj.manga_legacy_id = m.id
LEFT JOIN tags_json tj ON tj.manga_legacy_id = m.id
LEFT JOIN manga om ON om.url = m.url
WHERE
    m.deleted_at IS NULL
    AND om.id IS NULL
ORDER BY m.id;