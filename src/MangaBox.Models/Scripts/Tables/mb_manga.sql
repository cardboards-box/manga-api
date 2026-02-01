CREATE TABLE IF NOT EXISTS mb_manga (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	title TEXT NULL,
	alt_titles TEXT[] DEFAULT '{}',
	description TEXT NULL,
	alt_descriptions TEXT[] DEFAULT '{}',
	url TEXT NOT NULL,
	attributes mb_attribute[] DEFAULT '{}',
	content_rating INTEGER NOT NULL,
	nsfw BOOLEAN NOT NULL,
	source_id UUID NOT NULL REFERENCES mb_sources(id),
	original_source_id TEXT NOT NULL,
	is_hidden BOOLEAN NOT NULL,
	referer TEXT NULL,
	user_agent TEXT NULL,
	source_created TIMESTAMP NULL,
	ordinal_volume_reset BOOLEAN NOT NULL,
	legacy_id INTEGER NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_manga_unique UNIQUE (source_id, original_source_id)
);

ALTER TABLE mb_manga
ADD COLUMN IF NOT EXISTS
	fts tsvector GENERATED ALWAYS AS (
		to_tsvector('english',
			title || ' ' ||
			description
		)
	) STORED;
