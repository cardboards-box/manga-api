namespace CardboardBox.Manga.Database;

public interface IMangaWithChaptersDbService
{
    Task<MangaWithChapters?> Get(string id, string? platformId);

    Task<MangaWithChapters?> GetById(long id, string? platformId);

    Task<MangaWithChapters?> GetByHashId(string hashId, string? platformId);

    Task<MangaWithChapters?> GetByRandom(string? platformId);
}

public class MangaWithChaptersDbService : IMangaWithChaptersDbService
{
    private readonly ISqlService _sql;

    public MangaWithChaptersDbService(ISqlService sql)
    {
        _sql = sql;
    }

    public Task<MangaWithChapters?> Get(string id, string? platformId)
    {
        return long.TryParse(id, out var lid)
            ? GetById(lid, platformId)
            : GetByHashId(id, platformId);
    }

    public Task<MangaWithChapters?> GetById(long id, string? platformId)
    {
        const string QUERY = "SELECT * FROM manga WHERE id = :id;" +
            "SELECT * FROM manga_chapter WHERE manga_id = :id ORDER BY ordinal ASC, created_at ASC;";
        const string TARGETED_QUERY = @"SELECT mb.* 
FROM manga_bookmarks mb
JOIN profiles p ON p.id = mb.profile_id
WHERE p.platform_id = :platformId AND mb.manga_id = :id
ORDER BY mb.manga_chapter_id;

SELECT 1 
FROM manga_favourites mf 
JOIN profiles p ON p.id = mf.profile_id
WHERE p.platform_id = :platformId AND mf.manga_id = :id";
        return GetByQueries(QUERY, TARGETED_QUERY, new { id }, platformId);
    }

    public Task<MangaWithChapters?> GetByHashId(string hashId, string? platformId)
    {
        const string QUERY = "SELECT * FROM manga WHERE hash_id = :id;" +
            @"SELECT c.* FROM manga_chapter c
JOIN manga m ON m.id = c.manga_id
WHERE m.hash_id = :id ORDER BY c.ordinal ASC, c.created_at ASC;";
        const string TARGETED_QUERY = @"SELECT mb.* 
FROM manga_bookmarks mb
JOIN manga m ON m.id = mb.manga_id
JOIN profiles p ON p.id = mb.profile_id
WHERE p.platform_id = :platformId AND m.hash_id = :id
ORDER BY mb.manga_chapter_id;

SELECT 1 
FROM manga_favourites mf 
JOIN manga m ON m.id = mf.manga_id
JOIN profiles p ON p.id = mf.profile_id
WHERE p.platform_id = :platformId AND m.hash_id = :id";

        return GetByQueries(QUERY, TARGETED_QUERY, new { id = hashId }, platformId);
    }

    public async Task<MangaWithChapters?> GetByRandom(string? platformId)
    {
        const string RANDOM_QUERY = "SELECT * FROM manga ORDER BY random() LIMIT 1;";
        var manga = await _sql.Fetch<DbManga>(RANDOM_QUERY);
        if (manga == null) return null;

        return await GetById(manga.Id, platformId);
    }

    public async Task<MangaWithChapters?> GetByQueries(string notTargetQuery, string targetQuery, object parameters, string? platformId)
    {
        var pars = new DynamicParameters(parameters);
        if (!string.IsNullOrEmpty(platformId))
            pars.Add("platformId", platformId);

        var query = string.IsNullOrEmpty(platformId) ? notTargetQuery : notTargetQuery + targetQuery;

        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(query, pars);

        var manga = await rdr.ReadFirstOrDefaultAsync<DbManga>();
        if (manga == null) return null;

        var chapters = await rdr.ReadAsync<DbMangaChapter>();
        if (string.IsNullOrEmpty(platformId))
            return new(manga, chapters.ToArray());

        var bookmarks = await rdr.ReadAsync<DbMangaBookmark>();
        var favourite = (await rdr.ReadSingleOrDefaultAsync<bool?>()) ?? false;

        return new(manga, chapters.ToArray(), bookmarks.ToArray(), favourite);
    }
}
