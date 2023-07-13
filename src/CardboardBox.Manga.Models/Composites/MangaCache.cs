namespace CardboardBox.Manga.Models.Composites;

using Tables;

public record class MangaCache(
    DbMangaCache Manga, 
    DbMangaChapterCache Chapter, 
    DbManga? CbaManga, 
    DbMangaChapter? CbaMangaChapter);