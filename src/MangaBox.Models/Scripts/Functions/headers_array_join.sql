CREATE OR REPLACE
FUNCTION headers_array_join (
	mb_headers[],
	text
) RETURNS text LANGUAGE sql
IMMUTABLE AS $$
	SELECT array_to_string(ARRAY(
		SELECT elem.value
		FROM unnest($1) AS elem
		WHERE elem.value IS NOT NULL
	), $2)
$$;
