CREATE TABLE IF NOT EXISTS mb_logins (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	profile_id UUID NOT NULL REFERENCES mb_profiles(id),
	platform_id TEXT NOT NULL UNIQUE,
	username TEXT NOT NULL,
	avatar TEXT NOT NULL,
	provider TEXT NOT NULL,
	provider_id TEXT NOT NULL,
	email TEXT NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP
);