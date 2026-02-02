CREATE TABLE IF NOT EXISTS mb_manga_relationships (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	manga_id UUID NOT NULL REFERENCES mb_manga(id),
	person_id UUID NOT NULL REFERENCES mb_people(id),
	type INTEGER NOT NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL,
	CONSTRAINT mb_manga_relationships_unique UNIQUE (manga_id, person_id, type)
);
