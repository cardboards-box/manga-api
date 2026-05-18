DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1
		FROM pg_type
		WHERE typname = 'mb_image_slice'
	) THEN
		CREATE TYPE mb_image_slice AS (
			image UUID,
			ordinal INTEGER,
			start INTEGER,
			stop INTEGER
		);
	END IF;
END$$;
