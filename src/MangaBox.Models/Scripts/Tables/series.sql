CREATE TABLE IF NOT EXISTS mb_series (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	provider_id uuid NOT NULL REFERENCES mb_providers(id),
	source_id TEXT NOT NULL,
	cover_id uuid NOT NULL REFERENCES mb_images(id),
	rating_id uuid NOT NULL REFERENCES mb_content_ratings(id),
	tags uuid[] NOT NULL DEFAULT '{}',
	title TEXT NOT NULL,
	display_title TEXT,
	alt_titles TEXT[] NOT NULL DEFAULT '{}',
	description TEXT NOT NULL,
	url TEXT NOT NULL,
	status INTEGER NOT NULL,
	ordinals_reset BOOLEAN NOT NULL DEFAULT FALSE,
	source_created TIMESTAMP,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_series UNIQUE (provider_id, source_id)
);