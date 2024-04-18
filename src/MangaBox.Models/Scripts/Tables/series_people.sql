CREATE TABLE IF NOT EXISTS mb_series_people (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	series_id uuid NOT NULL REFERENCES mb_series(id),
	person_id uuid NOT NULL REFERENCES mb_people(id),
	type INTEGER NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_series_person UNIQUE (series_id, person_id, type)
);