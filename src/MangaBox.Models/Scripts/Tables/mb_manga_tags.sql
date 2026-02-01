CREATE TABLE IF NOT EXISTS mb_manga_tags (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	manga_id UUID NOT NULL REFERENCES mb_manga(id),
	tag_id UUID NOT NULL REFERENCES mb_tags(id),
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_manga_tags_unique UNIQUE (manga_id, tag_id)
);
