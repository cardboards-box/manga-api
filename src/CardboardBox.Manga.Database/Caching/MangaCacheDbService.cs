namespace CardboardBox.Manga.Database.Caching;

using Base;

public interface IMangaCacheDbService : IOrmMap<DbMangaCache>
{
    Task<DbMangaCache[]> ByIds(string[] mangaIds);

    Task<DbMangaCache[]> BadCoverArt();
}

public class MangaCacheDbService : OrmMap<DbMangaCache>, IMangaCacheDbService
{
    public MangaCacheDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    public Task<DbMangaCache[]> ByIds(string[] mangaIds)
    {
        const string QUERY = @"SELECT
	DISTINCT
	*
FROM manga_cache
WHERE source_id = ANY(:mangaIds)";
        return _sql.Get<DbMangaCache>(QUERY, new { mangaIds });
    }

    public Task<DbMangaCache[]> BadCoverArt()
    {
        const string QUERY = "SELECT * FROM manga_cache WHERE cover LIKE '%/';";
        return _sql.Get<DbMangaCache>(QUERY);
    }
}
