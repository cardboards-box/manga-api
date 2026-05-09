CREATE TABLE IF NOT EXISTS mb_notification_subscriptions (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	profile_id UUID NOT NULL REFERENCES mb_profiles(id),
	manga_id UUID NULL REFERENCES mb_manga(id),
	person_id UUID NULL REFERENCES mb_people(id),
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_notification_subscriptions_unique UNIQUE NULLS NOT DISTINCT (profile_id, manga_id, person_id)
);
