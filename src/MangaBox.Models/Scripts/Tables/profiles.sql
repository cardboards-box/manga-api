CREATE TABLE IF NOT EXISTS mb_profiles (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	role_ids UUID[] NOT NULL DEFAULT '{}',
	settings_blob TEXT,
	primary_user UUID,
	nickname TEXT NOT NULL,
	avatar TEXT NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP
);