CREATE OR REPLACE VIEW manga_attributes
AS
    SELECT
        DISTINCT
        id,
        nsfw,
        (unnest(attributes)).name as name,
        (unnest(attributes)).value as value
    FROM manga;