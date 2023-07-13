DO $$ BEGIN

	IF (to_regtype('manga_chapter_progress') IS NULL) THEN
		CREATE TYPE manga_chapter_progress AS (
			chapter_id BIGINT,
			page_index INT
		);
	END IF;

END $$;