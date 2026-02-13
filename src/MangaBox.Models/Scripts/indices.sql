
--Index on full-text search
CREATE INDEX IF NOT EXISTS idx_manga_fts ON mb_manga USING GIN (fts);
CREATE INDEX IF NOT EXISTS ix_mb_manga_title_trgm ON mb_manga USING GIN (title gin_trgm_ops) WHERE deleted_at IS NULL;

--Create FK indices
CREATE INDEX IF NOT EXISTS idx_manga_source_id ON mb_manga (source_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_manga_ex_manga_id ON mb_manga_ext (manga_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_manga_ex_chapter_first ON mb_manga_ext (first_chapter_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_manga_ex_chapter_last ON mb_manga_ext (last_chapter_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_manga_progress_fk ON mb_manga_progress (profile_id, manga_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_manga_tags_fk ON mb_manga_tags (manga_id, tag_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_tags_source ON mb_tags (source_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_people_profile ON mb_people (profile_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_image_manga ON mb_images (manga_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_image_chapter ON mb_images (chapter_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_mb_manga_rel_manga ON mb_manga_relationships(manga_id);
CREATE INDEX IF NOT EXISTS ix_mb_manga_rel_person_type ON mb_manga_relationships(person_id, type);

--Create created_at indices
CREATE INDEX IF NOT EXISTS idx_manga_created_at ON mb_manga (created_at) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_chapters_created_at ON mb_chapters (created_at) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_images_created_at ON mb_images (created_at) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_sources_created_at ON mb_sources (created_at) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_people_created_at ON mb_people (created_at) WHERE deleted_at IS NULL;