CREATE TABLE IF NOT EXISTS mb_images (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	url TEXT NULL,
	chapter_id UUID NULL REFERENCES mb_chapters(id),
	manga_id UUID NOT NULL REFERENCES mb_manga(id),
	ordinal INTEGER NOT NULL,
	file_name TEXT NULL,
	url_hash TEXT NULL,
	image_width INTEGER NULL,
	image_height INTEGER NULL,
	image_size BIGINT NULL,
	mime_type TEXT NULL,
	indexed BOOLEAN NOT NULL DEFAULT FALSE,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_images_unique UNIQUE NULLS NOT DISTINCT (chapter_id, manga_id, ordinal)
);
