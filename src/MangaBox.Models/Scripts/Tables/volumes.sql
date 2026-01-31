CREATE TABLE IF NOT EXISTS mb_volumes (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	series_id uuid NOT NULL REFERENCES mb_series(id),
	ordinal NUMERIC NOT NULL,
	title TEXT NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	created_by UUID NOT NULL REFERENCES mb_people(id),
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_by UUID NOT NULL REFERENCES mb_people(id),
	deleted_at TIMESTAMP,
	deleted_by UUID REFERENCES mb_people(id),

	CONSTRAINT unique_volumes UNIQUE (series_id, ordinal)
);