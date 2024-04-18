CREATE TABLE IF NOT EXISTS mb_chapters (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	volume_id uuid NOT NULL REFERENCES mb_volumes(id),
	source_id TEXT NOT NULL,
	uploader_id uuid NOT NULL REFERENCES mb_people(id),
	ordinal NUMERIC NOT NULL,
	title TEXT NOT NULL,
	url TEXT NOT NULL,
	external_url TEXT,
	language TEXT NOT NULL,
	pages_loaded BOOLEAN NOT NULL DEFAULT FALSE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_chapter UNIQUE (volume_id, source_id)
);