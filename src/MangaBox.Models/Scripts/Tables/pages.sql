CREATE TABLE IF NOT EXISTS mb_pages (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	chapter_id uuid NOT NULL REFERENCES mb_chapters(id),
	image_id uuid NOT NULL REFERENCES mb_images(id),
	ordinal NUMERIC NOT NULL,
	type INTEGER NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_pages UNIQUE (chapter_id, image_id)
);