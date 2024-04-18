CREATE TABLE IF NOT EXISTS mb_volumes (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	
	series_id uuid NOT NULL REFERENCES mb_series(id),
	ordinal NUMERIC NOT NULL,
	title TEXT NOT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP,

	CONSTRAINT unique_volumes UNIQUE (series_id, ordinal)
);