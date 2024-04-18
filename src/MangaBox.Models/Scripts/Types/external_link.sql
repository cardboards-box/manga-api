DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1
		FROM pg_type
		WHERE typname = 'mb_external_link'
	) THEN
		CREATE TYPE mb_external_link AS (
			platform TEXT,
			url TEXT
		);
	END IF;

END$$;