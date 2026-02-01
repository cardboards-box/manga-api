CREATE OR REPLACE FUNCTION text_array_join(text[], text)
RETURNS text
LANGUAGE sql
IMMUTABLE AS $$SELECT array_to_string($1, $2)$$;