DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1
		FROM pg_type
		WHERE typname = 'mb_headers'
	) THEN
		CREATE TYPE mb_headers AS (
			key TEXT,
			value TEXT
		);
	END IF;
END$$;
