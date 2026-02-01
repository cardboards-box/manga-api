CREATE TABLE IF NOT EXISTS mb_manga_progress (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	profile_id UUID NOT NULL REFERENCES mb_profiles(id),
	manga_id UUID NOT NULL REFERENCES mb_manga(id),
	last_read_ordinal NUMERIC NULL,
	last_read_chapter_id UUID NULL REFERENCES mb_chapters(id),
	is_completed BOOLEAN NOT NULL,
	favorited BOOLEAN NOT NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_manga_progress_unique UNIQUE NULLS NOT DISTINCT (profile_id, manga_id)
);
