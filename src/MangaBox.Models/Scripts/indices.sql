
--Index on full-text search
CREATE INDEX IF NOT EXISTS idx_manga_fts ON mb_manga USING GIN (fts);

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