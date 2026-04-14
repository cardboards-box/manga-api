CREATE TABLE IF NOT EXISTS mb_api_keys (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	profile_id UUID NOT NULL REFERENCES mb_profiles(id),
	name TEXT NOT NULL,
	key TEXT NOT NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_api_keys_unique UNIQUE (profile_id, name)
);
