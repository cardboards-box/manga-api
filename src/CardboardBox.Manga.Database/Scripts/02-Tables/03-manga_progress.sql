CREATE TABLE IF NOT EXISTS manga_progress (
    id BIGSERIAL PRIMARY KEY,
    
    profile_id bigint not null references profiles(id),
    manga_id bigint not null references manga(id),
    manga_chapter_id bigint references manga_chapter(id),
    page_index int,
    read manga_chapter_progress[] not null default '{}',

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_progress UNIQUE(profile_id, manga_id)
);