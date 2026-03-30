CREATE TABLE IF NOT EXISTS mb_logs (
	id UUID DEFAULT uuid_generate_v4() PRIMARY KEY,
	log_level INTEGER NOT NULL,
	source TEXT NULL,
	category TEXT NULL,
	message TEXT NULL,
	exception TEXT NULL,
	context TEXT NULL,
	created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	deleted_at TIMESTAMP NULL
);

ALTER TABLE mb_logs
ADD COLUMN IF NOT EXISTS
	fts tsvector GENERATED ALWAYS AS (
		to_tsvector('english',
			category || ' ' ||
			source || ' ' ||
			message || ' ' ||
			exception || ' ' ||
			context
		)
	) STORED;