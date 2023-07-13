DO $$ BEGIN

    IF (to_regtype('manga_attribute') IS NULL) THEN
		CREATE TYPE manga_attribute AS (
			name text,
			value text
		);
	END IF;

END $$;