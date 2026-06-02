CREATE OR REPLACE FUNCTION mb_upsert_manga_covers(
    p_manga_id uuid,
    p_covers jsonb
) RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_min_ordinal integer;
BEGIN
    DROP TABLE IF EXISTS mb_cover_desired_tmp;
    DROP TABLE IF EXISTS mb_cover_moved_tmp;
    DROP TABLE IF EXISTS mb_cover_matched_tmp;

    CREATE TEMP TABLE mb_cover_desired_tmp ON COMMIT DROP AS
    SELECT
        cover_url,
        row_number() OVER (ORDER BY first_ordinal)::int AS ordinal
    FROM (
        SELECT
            NULLIF(value, '') AS cover_url,
            MIN(ord) AS first_ordinal
        FROM jsonb_array_elements_text(COALESCE(p_covers, '[]'::jsonb)) WITH ORDINALITY AS cover(value, ord)
        WHERE NULLIF(value, '') IS NOT NULL
        GROUP BY NULLIF(value, '')
    ) covers;

    IF NOT EXISTS (SELECT 1 FROM mb_cover_desired_tmp) THEN
        RETURN;
    END IF;

    SELECT COALESCE(MIN(ordinal), 0)
    INTO v_min_ordinal
    FROM mb_images
    WHERE
        manga_id = p_manga_id AND
        chapter_id IS NULL;

    CREATE TEMP TABLE mb_cover_moved_tmp ON COMMIT DROP AS
    SELECT
        img.id,
        (v_min_ordinal - row_number() OVER (ORDER BY img.ordinal, img.id))::int AS temp_ordinal
    FROM mb_images img
    WHERE
        img.manga_id = p_manga_id AND
        img.chapter_id IS NULL;

    UPDATE mb_images img
    SET
        ordinal = moved.temp_ordinal,
        updated_at = CURRENT_TIMESTAMP
    FROM mb_cover_moved_tmp moved
    WHERE img.id = moved.id;

    CREATE TEMP TABLE mb_cover_matched_tmp ON COMMIT DROP AS
    SELECT DISTINCT ON (desired.cover_url)
        img.id,
        desired.cover_url,
        desired.ordinal
    FROM mb_cover_desired_tmp desired
    JOIN mb_images img
        ON img.manga_id = p_manga_id
       AND img.chapter_id IS NULL
       AND img.url = desired.cover_url
    ORDER BY
        desired.cover_url,
        (img.deleted_at IS NOT NULL),
        img.created_at,
        img.id;

    UPDATE mb_images img
    SET
        ordinal = matched.ordinal,
        updated_at = CURRENT_TIMESTAMP,
        deleted_at = NULL
    FROM mb_cover_matched_tmp matched
    WHERE
        img.id = matched.id;

    INSERT INTO mb_images (
        url,
        chapter_id,
        manga_id,
        ordinal,
        updated_at
    )
    SELECT
        desired.cover_url,
        NULL::uuid,
        p_manga_id,
        desired.ordinal,
        CURRENT_TIMESTAMP
    FROM mb_cover_desired_tmp desired
    WHERE NOT EXISTS (
        SELECT 1
        FROM mb_cover_matched_tmp matched
        WHERE
            matched.cover_url = desired.cover_url
    )
    ON CONFLICT ON CONSTRAINT mb_images_unique
    DO NOTHING;

    UPDATE mb_images img
    SET
        ordinal = moved.ordinal,
        updated_at = CURRENT_TIMESTAMP
    FROM (
        SELECT
            img.id,
            (
                COALESCE((
                    SELECT MAX(i2.ordinal)
                    FROM mb_images i2
                    WHERE
                        i2.manga_id = p_manga_id AND
                        i2.chapter_id IS NULL AND
                        i2.ordinal > 0
                ), 0) + row_number() OVER (ORDER BY img.ordinal, img.id)
            )::int AS ordinal
        FROM mb_images img
        JOIN mb_cover_moved_tmp moved ON moved.id = img.id
        LEFT JOIN mb_cover_matched_tmp matched ON matched.id = img.id
        WHERE
            matched.id IS NULL
    ) moved
    WHERE img.id = moved.id;
END;
$$;
