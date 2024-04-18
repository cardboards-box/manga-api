CREATE TABLE IF NOT EXISTS mb_people (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	provider_id uuid NOT NULL REFERENCES mb_providers(id),
	source_id TEXT NOT NULL,
	name TEXT NOT NULL,
	links mb_external_link[] NOT NULL DEFAULT '{}',
	type INTEGER NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_person UNIQUE (provider_id, source_id)
);