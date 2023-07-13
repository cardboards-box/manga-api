CREATE TABLE IF NOT EXISTS manga_bookmarks (
    id BIGSERIAL PRIMARY KEY,

    profile_id bigint not null references profiles(id),
    manga_id bigint not null references manga(id),
    manga_chapter_id bigint not null references manga_chapter(id),
    pages int[] not null default '{}',

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_bookmarks UNIQUE(profile_id, manga_id, manga_chapter_id)
);