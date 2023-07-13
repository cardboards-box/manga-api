using CardboardBox.Manga.Models.Tables;

namespace CardboardBox.Manga.Database;

public interface IMangaExtendedDbService
{
    /// <summary>
    /// Fetches the extended manga information by either the <see cref="DbManga.Id"/> or the <see cref="DbManga.HashId"/>
    /// </summary>
    /// <param name="id">Either the <see cref="DbManga.Id"/> or the <see cref="DbManga.HashId"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    Task<MangaExtended?> Fetch(string id, string? platformId);

    /// <summary>
    /// Fetches the extended manga information by the <see cref="DbManga.Id"/>
    /// </summary>
    /// <param name="mangaId">The <see cref="DbManga.Id"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    Task<MangaExtended?> FetchById(long mangaId, string? platformId);

    /// <summary>
    /// Fetches the extended manga information by the <see cref="DbManga.HashId"/>
    /// </summary>
    /// <param name="hashId">The <see cref="DbManga.HashId"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    Task<MangaExtended?> FetchByHashId(string hashId, string? platformId);

    /// <summary>
    /// Gets all of the manga that have been updated since the given date.
    /// If a <paramref name="platformId"/> is specified, it will only return any manga that the user is currently reading.
    /// </summary>
    /// <param name="platformId">The current user's platform Id</param>
    /// <param name="since">The date to get the manga since</param>
    /// <param name="page">The current page of records</param>
    /// <param name="size">The maximum size of returned records</param>
    /// <returns></returns>
    Task<PaginatedResult<MangaExtended>> Since(string? platformId, DateTime since, int page, int size);
}

public class MangaExtendedDbService : IMangaExtendedDbService
{
    private readonly ISqlService _sql;

    public MangaExtendedDbService(ISqlService sql)
    {
        _sql = sql;
    }

    /// <summary>
    /// Fetches the extended manga information by either the <see cref="DbManga.Id"/> or the <see cref="DbManga.HashId"/>
    /// </summary>
    /// <param name="id">Either the <see cref="DbManga.Id"/> or the <see cref="DbManga.HashId"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    public Task<MangaExtended?> Fetch(string id, string? platformId)
    {
        return long.TryParse(id, out var lid)
            ? Fetch(null, lid, platformId)
            : Fetch(id, null, platformId);
    }

    /// <summary>
    /// Fetches the extended manga information by the <see cref="DbManga.Id"/>
    /// </summary>
    /// <param name="mangaId">The <see cref="DbManga.Id"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    public Task<MangaExtended?> FetchById(long mangaId, string? platformId)
    {
        return Fetch(null, mangaId, platformId);
    }

    /// <summary>
    /// Fetches the extended manga information by the <see cref="DbManga.HashId"/>
    /// </summary>
    /// <param name="hashId">The <see cref="DbManga.HashId"/></param>
    /// <param name="platformId">The current user's platform Id</param>
    /// <returns></returns>
    public Task<MangaExtended?> FetchByHashId(string hashId, string? platformId)
    {
        return Fetch(hashId, null, platformId);
    }

    public async Task<MangaExtended?> Fetch(string? hashId, long? id, string? platformId)
    {
        const string QUERY = @"SELECT
    m.*,
    '' as split,
    mp.*,
    '' as split,
    mc.*,
    '' as split,
    t.*
FROM get_manga_filtered( :platformId , 99, ARRAY(
	SELECT
	    id
    FROM manga
    WHERE
        hash_id = :hashId OR id = :id
)) t
JOIN manga m ON m.id = t.manga_id
JOIN manga_chapter mc ON mc.id = t.manga_chapter_id
LEFT JOIN manga_progress mp ON mp.id = t.progress_id";

        using var con = await _sql.CreateConnection();
        var records = await con.QueryAsync<DbManga, DbMangaProgress, DbMangaChapter, MangaStats, MangaExtended>(
            QUERY, (m, p, c, s) => new MangaExtended(m, p, c, s),
            param: new { hashId, id, platformId }, splitOn: "split");
        return records.FirstOrDefault();
    }

    /// <summary>
    /// Gets all of the manga that have been updated since the given date.
    /// If a <paramref name="platformId"/> is specified, it will only return any manga that the user is currently reading.
    /// </summary>
    /// <param name="platformId">The current user's platform Id</param>
    /// <param name="since">The date to get the manga since</param>
    /// <param name="page">The current page of records</param>
    /// <param name="size">The maximum size of returned records</param>
    /// <returns></returns>
    public async Task<PaginatedResult<MangaExtended>> Since(string? platformId, DateTime since, int page, int size)
    {
        const string QUERY = @"CREATE TEMP TABLE touched_manga AS
SELECT
    t.*
FROM get_manga(:platformId, :state) t
WHERE 
    t.latest_chapter >= :since;

SELECT
    m.*,
    '' as split,
    mp.*,
    '' as split,
    mc.*,
    '' as split,
    t.*
FROM touched_manga t
JOIN manga m ON m.id = t.manga_id
JOIN manga_chapter mc ON mc.id = t.manga_chapter_id
LEFT JOIN manga_progress mp ON mp.id = t.progress_id
ORDER BY t.latest_chapter DESC
LIMIT :size OFFSET :offset;

SELECT COUNT(*) FROM touched_manga;

DROP TABLE touched_manga;";

        var state = string.IsNullOrEmpty(platformId) ? TouchedState.All : TouchedState.InProgress;
        var offset = (page - 1) * size;
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { platformId, state = (int)state, offset, size, since });

        var results = rdr.Read<DbManga, DbMangaProgress, DbMangaChapter, MangaStats, MangaExtended>((m, p, c, s) => new MangaExtended(m, p, c, s), splitOn: "split");
        var total = await rdr.ReadSingleAsync<int>();
        var pages = (int)Math.Ceiling((double)total / size);
        return new PaginatedResult<MangaExtended>(pages, total, results.ToArray());
    }
}
