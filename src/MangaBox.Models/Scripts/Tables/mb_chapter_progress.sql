CREATE TABLE IF NOT EXISTS mb_chapter_progress (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	progress_id UUID NOT NULL REFERENCES mb_manga_progress(id),
	chapter_id UUID NOT NULL REFERENCES mb_chapters(id),
	is_read BOOLEAN NOT NULL,
	bookmarks INTEGER[] DEFAULT '{}',
	last_read TIMESTAMP NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_chapter_progress_unique UNIQUE NULLS NOT DISTINCT (progress_id, chapter_id)
);
