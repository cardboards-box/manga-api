namespace CardboardBox.Manga.Models;

public record class MangaCache(
    DbMangaCache Manga, 
    DbMangaChapterCache Chapter, 
    DbManga? CbaManga, 
    DbMangaChapter? CbaMangaChapter);