CREATE TABLE IF NOT EXISTS manga_favourites (
    id BIGSERIAL PRIMARY KEY,

    profile_id bigint not null references profiles(id),
    manga_id bigint not null references manga(id),

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_favourites UNIQUE(profile_id, manga_id)
);