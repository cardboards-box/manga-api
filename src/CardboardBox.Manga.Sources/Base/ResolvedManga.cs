namespace CardboardBox.Manga.Sources;

using Models;

public record class ResolvedManga(DbManga Manga, DbMangaChapter[] Chapters);
