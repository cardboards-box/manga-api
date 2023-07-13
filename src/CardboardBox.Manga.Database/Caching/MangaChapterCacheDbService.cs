namespace CardboardBox.Manga.Database.Caching;

using Base;

public interface IMangaChapterCacheDbService : IOrmMap<DbMangaChapterCache> { }

internal class MangaChapterCacheDbService : OrmMap<DbMangaChapterCache>, IMangaChapterCacheDbService
{
    public MangaChapterCacheDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }
}
