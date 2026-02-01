CREATE TABLE IF NOT EXISTS mb_chapters (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	manga_id UUID NOT NULL REFERENCES mb_manga(id),
	title TEXT NULL,
	url TEXT NULL,
	source_id TEXT NOT NULL,
	ordinal NUMERIC NOT NULL,
	volume NUMERIC NULL,
	language TEXT NULL,
	external_url TEXT NULL,
	attributes mb_attribute[] DEFAULT '{}',
	legacy_id INTEGER NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_chapters_unique UNIQUE NULLS NOT DISTINCT (manga_id, source_id)
);
