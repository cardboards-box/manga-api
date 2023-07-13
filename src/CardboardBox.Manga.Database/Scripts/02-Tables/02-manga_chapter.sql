CREATE TABLE IF NOT EXISTS manga_chapter (
    id BIGSERIAL PRIMARY KEY,

    manga_id bigint not null references manga(id),
    title text not null,
    url text not null,
    source_id text not null,
    ordinal numeric not null,
    volume numeric,
    language text not null,
    pages text[] not null default '{}',
    external_url text,
    attributes manga_attribute[] not null default '{}',

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_chapter UNIQUE(manga_id, source_id, language)
);