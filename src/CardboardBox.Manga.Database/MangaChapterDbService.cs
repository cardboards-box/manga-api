namespace CardboardBox.Manga.Database;

using Base;

public interface IMangaChapterDbService : IOrmMap<DbMangaChapter>
{
    Task SetPages(long id, string[] pages);
}

public class MangaChapterDbService : OrmMap<DbMangaChapter>, IMangaChapterDbService
{
    public MangaChapterDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    public Task SetPages(long id, string[] pages)
    {
        const string QUERY = "UPDATE manga_chapter SET pages = :pages WHERE id = :id";
        return _sql.Execute(QUERY, new { id, pages });
    }
}
