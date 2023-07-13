namespace CardboardBox.Manga.Database;

using Base;

public interface IMangaProgressDbService : IOrmMap<DbMangaProgress>
{
    /// <summary>
    /// Fetches the <see cref="DbMangaProgress"/> by either the <see cref="DbManga.HashId"/> or <see cref="DbManga.Id"/>
    /// </summary>
    /// <param name="mangaId">Either the <see cref="DbManga.HashId"/> or <see cref="DbManga.Id"/></param>
    /// <param name="platformId">The current user's platformId</param>
    /// <returns></returns>
    Task<DbMangaProgress?> Fetch(string mangaId, string? platformId);

    Task DeleteByManga(long profileId, long mangaId);
}

public class MangaProgressDbService : OrmMap<DbMangaProgress>, IMangaProgressDbService
{
    private static string? _getProgress;
    private static string? _insertProgress;
    private static string? _updateProgress;

    public MangaProgressDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    /// <summary>
    /// Fetches the <see cref="DbMangaProgress"/> by either the <see cref="DbManga.HashId"/> or <see cref="DbManga.Id"/>
    /// </summary>
    /// <param name="mangaId">Either the <see cref="DbManga.HashId"/> or <see cref="DbManga.Id"/></param>
    /// <param name="platformId">The current user's platformId</param>
    /// <returns></returns>
    public Task<DbMangaProgress?> Fetch(string mangaId, string? platformId)
    {
        return long.TryParse(mangaId, out var lid)
            ? FetchByMangaId(lid, platformId)
            : FetchByHashId(mangaId, platformId);
    }

    /// <summary>
    /// Fetches the <see cref="DbMangaProgress"/> by the <see cref="DbManga.Id"/>
    /// </summary>
    /// <param name="mangaId">The <see cref="DbManga.Id"/></param>
    /// <param name="platformId">The current user's platformId</param>
    /// <returns></returns>
    public Task<DbMangaProgress?> FetchByMangaId(long mangaId, string? platformId)
    {
        const string QUERY = @"SELECT 
	mp.*
FROM manga_progress mp
JOIN profiles p ON p.id = mp.profile_id
WHERE
	p.platform_id = :platformId AND
	mp.manga_id = :mangaId AND
	mp.deleted_at IS NULL AND
	p.deleted_at IS NULL";
        return _sql.Fetch<DbMangaProgress?>(QUERY, new { platformId, mangaId });
    }

    /// <summary>
    /// Fetches the <see cref="DbMangaProgress"/> by the <see cref="DbManga.HashId"/>
    /// </summary>
    /// <param name="hashId">The <see cref="DbManga.HashId"/></param>
    /// <param name="platformId">The current user's platformId</param>
    /// <returns></returns>
    public Task<DbMangaProgress?> FetchByHashId(string hashId, string? platformId)
    {
        const string QUERY = @"SELECT 
	mp.*
FROM manga_progress mp
JOIN manga m ON mp.manga_id = m.id
JOIN profiles p ON p.id = mp.profile_id
WHERE
	p.platform_id = :platformId AND
	m.hash_id = :hashId AND
	mp.deleted_at IS NULL AND
	p.deleted_at IS NULL";
        return _sql.Fetch<DbMangaProgress?>(QUERY, new { platformId, hashId });
    }

    public override async Task<long> Upsert(DbMangaProgress progress)
    {
        if (string.IsNullOrEmpty(_getProgress) ||
            string.IsNullOrEmpty(_insertProgress) ||
            string.IsNullOrEmpty(_updateProgress))
        {
            var (insert, update, select) = _fakeUpserts.FakeUpsert<DbMangaProgress>();
            _getProgress = select;
            _insertProgress = insert;
            _updateProgress = update;
        }

        var exists = await _sql.Fetch<DbMangaProgress>(_getProgress, new { progress.ProfileId, progress.MangaId });
        if (exists == null)
        {
            if (progress.MangaChapterId != null && progress.PageIndex != null)
                progress.Read = new[]
                {
                    new DbMangaChapterProgress(progress.MangaChapterId.Value, progress.PageIndex.Value)
                };
            return await _sql.ExecuteScalar<long>(_insertProgress, progress);
        }

        var pages = exists.Read;
        if (progress.MangaChapterId != null &&
            progress.PageIndex != null)
        {
            var cur = new DbMangaChapterProgress(progress.MangaChapterId.Value, progress.PageIndex.Value);
            var found = false;
            pages = exists.Read.Select(t =>
            {
                if (t.ChapterId != progress.MangaChapterId) return t;
                found = true;

                if (t.PageIndex > progress.PageIndex) return t;

                return cur;
            }).ToArray();

            if (!found)
                pages = pages.Append(cur).ToArray();
        }

        progress.Id = exists.Id;
        progress.Read = pages.OrderBy(t => t.ChapterId).ToArray();

        var res = await _sql.Execute(_updateProgress, progress);
        return exists.Id;
    }

    public Task DeleteByManga(long profileId, long mangaId)
    {
        const string QUERY = @"UPDATE manga_progress 
SET 
	manga_chapter_id = NULL, 
	page_index = NULL 
WHERE 
	profile_id = :profileId AND 
	manga_id = :mangaId";
        return _sql.Execute(QUERY, new { profileId, mangaId });
    }
}
