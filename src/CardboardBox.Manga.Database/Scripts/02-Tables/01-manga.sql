CREATE TABLE IF NOT EXISTS manga (
    id BIGSERIAL PRIMARY KEY,

    title text not null,
    source_id text not null,
    provider text not null,
    hash_id text not null,
    url text not null,
    cover text not null,
    tags text[] not null default '{}',
    alt_titles text[] not null default '{}',
    description text not null,
    nsfw boolean not null default False,
    referer text,
    source_created timestamp,

    attributes manga_attribute[] not null default '{}',

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_title_hash UNIQUE(source_id, provider)
);

ALTER TABLE manga
    ADD COLUMN
        IF NOT EXISTS
            fts tsvector
                GENERATED ALWAYS AS (
                    to_tsvector('english',
                        title || ' ' || description || ' ' || f_ciarr2text(alt_titles)
                    )
                ) STORED;