CREATE TABLE IF NOT EXISTS mb_images (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	provider_id uuid NOT NULL REFERENCES mb_providers(id),
	url TEXT NOT NULL,
	url_hash TEXT NOT NULL UNIQUE,
	type INTEGER NOT NULL,
	name TEXT,
	hash TEXT,
	bytes INTEGER,
	width INTEGER,
	height INTEGER,
	mime_type TEXT,
	cached_at TIMESTAMP,
	expires TIMESTAMP,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP
);