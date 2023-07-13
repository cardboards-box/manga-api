CREATE OR REPLACE FUNCTION f_ciarr2text(text[]) RETURNS text LANGUAGE sql IMMUTABLE AS $$SELECT array_to_string($1, ',')$$;

