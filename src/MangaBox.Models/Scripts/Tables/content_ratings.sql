CREATE TABLE IF NOT EXISTS mb_content_ratings (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	name TEXT NOT NULL UNIQUE,
	description TEXT NOT NULL,
	tag_color TEXT NOT NULL,
	explicit BOOLEAN NOT NULL DEFAULT FALSE,
	pornographic BOOLEAN NOT NULL DEFAULT FALSE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP
);


WITH content_ratings_base (name, description, tag_color, explicit, pornographic) AS (
	VALUES
		('Safe', 'Content is safe for viewing anywhere', '#00FF00', FALSE, FALSE),
		('Suggestive', 'Content is safe just slightly questionable', '#0000FF', FALSE, FALSE),
		('Erotica', 'Is that a nipple I see?', '#FF0000', TRUE, FALSE),
		('Pornographic', 'Holy shit, it''s hentai!', '#FF0000', TRUE, TRUE)
)
INSERT INTO mb_content_ratings (name, description, tag_color, explicit, pornographic)
SELECT
	s.name, s.description, s.tag_color, s.explicit, s.pornographic
FROM content_ratings_base s
LEFT JOIN mb_content_ratings t ON s.name = t.name
WHERE t.id IS NULL;
