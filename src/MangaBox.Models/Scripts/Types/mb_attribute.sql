DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1
		FROM pg_type
		WHERE typname = 'mb_attribute'
	) THEN
		CREATE TYPE mb_attribute AS (
			name TEXT,
			value TEXT
		);
	END IF;
END$$;
