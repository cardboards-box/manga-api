namespace CardboardBox.Manga.Database.Caching;

public interface ICacheDbService
{
    Task<DbMangaCache[]> AllManga();

    Task<DbMangaChapterCache[]> AllChapters();

    Task<long> Upsert(DbMangaCache item);

    Task<long> Upsert(DbMangaChapterCache item);

    Task<MangaCache[]> DetermineExisting(string[] chapterIds);

    Task<DbMangaCache[]> ByIds(string[] ids);

    Task MergeUpdates();

    Task<DbMangaCache[]> BadCoverArt();
}

public class CacheDbService : ICacheDbService
{
    private readonly IMangaCacheDbService _manga;
    private readonly IMangaChapterCacheDbService _chapter;
    private readonly ISqlService _sql;

    public CacheDbService(
        ISqlService sql,
        IMangaCacheDbService manga,
        IMangaChapterCacheDbService chapter)
    {
        _sql = sql;
        _manga = manga;
        _chapter = chapter;
    }

    public Task<DbMangaCache[]> BadCoverArt() => _manga.BadCoverArt();

    public Task<long> Upsert(DbMangaCache item) => _manga.Upsert(item);

    public Task<long> Upsert(DbMangaChapterCache item) => _chapter.Upsert(item);

    public Task<DbMangaCache[]> AllManga() => _manga.Get();

    public Task<DbMangaChapterCache[]> AllChapters() => _chapter.Get();

    public async Task<MangaCache[]> DetermineExisting(string[] chapterIds)
    {
        const string QUERY = @"SELECT
	DISTINCT
    m.*,
    '' as split,
    mc.*,
    '' as split,
    om.*,
    '' as split,
    oc.*
FROM manga_cache m
JOIN manga_chapter_cache mc on m.id = mc.manga_id
LEFT JOIN manga om ON om.source_id = m.source_id AND om.provider = m.provider
LEFT JOIN manga_chapter oc on oc.source_id = mc.source_id AND oc.manga_id = om.id
WHERE
    mc.source_id = ANY(:chapterIds)";

        using var con = await _sql.CreateConnection();

        var records = await con.QueryAsync<DbMangaCache, DbMangaChapterCache, DbManga, DbMangaChapter, MangaCache>(
            sql: QUERY,
            map: (a, b, c, d) => new MangaCache(a, b,
                c.Title == null ? null : c,
                d.Title == null ? null : d),
            param: new { chapterIds },
            splitOn: "split");

        return records.ToArray();
    }

    public Task<DbMangaCache[]> ByIds(string[] ids) => _manga.ByIds(ids);

    public Task MergeUpdates()
    {
        const string QUERY = @"INSERT INTO manga_chapter
(
    manga_id,
    title,
    url,
    source_id,
    ordinal,
    volume,
    language,
    pages,
    external_url,
    created_at,
    updated_at
)
SELECT
    om.id as manga_id,
    mcc.title,
    mcc.url,
    mcc.source_id,
    mcc.ordinal,
    mcc.volume,
    mcc.language,
    mcc.pages,
    mcc.external_url,
    CURRENT_TIMESTAMP as created_at,
    CURRENT_TIMESTAMP as updated_at
FROM manga_chapter_cache mcc
JOIN manga_cache mc ON mc.id = mcc.manga_id
JOIN manga om ON om.source_id = mc.source_id AND om.provider = mc.provider
LEFT JOIN manga_chapter omc ON omc.source_id = mcc.source_id AND om.id = omc.manga_id
WHERE
    omc.id IS NULL;";
        return _sql.Execute(QUERY);
    }
}
