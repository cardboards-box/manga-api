namespace CardboardBox.Manga.Database;

using Base;

public interface IMangaBookmarkDbService
{
    Task<DbMangaBookmark[]> Bookmarks(long id, string? platformId);

    Task Bookmark(long id, long chapterId, int[] pages, string? platformId);
}

public class MangaBookmarkDbService : OrmMap<DbMangaBookmark>, IMangaBookmarkDbService
{
    private readonly IProfileDbService _profile;

    public MangaBookmarkDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts,
        IProfileDbService profile) : base(query, sql, fakeUpserts) 
    { 
        _profile = profile;
    }

    public Task<DbMangaBookmark[]> Bookmarks(long id, string? platformId)
    {
        if (string.IsNullOrEmpty(platformId)) return Task.FromResult(Array.Empty<DbMangaBookmark>());

        const string QUERY = @"SELECT mb.* FROM manga_bookmarks mb
JOIN profiles p ON p.id = mb.profile_id
WHERE p.platform_id = :platformId AND mb.manga_id = :id";
        return _sql.Get<DbMangaBookmark>(QUERY, new { id, platformId });
    }

    public async Task Bookmark(long id, long chapterId, int[] pages, string? platformId)
    {
        const string DELETE_QUERY = @"
DELETE FROM manga_bookmarks 
WHERE id IN (
	SELECT
		mb.id
	FROM manga_bookmarks mb 
	JOIN profiles p ON p.id = mb.profile_id
	WHERE p.platform_id = :platformId AND
		  mb.manga_id = :id AND
		  mb.manga_chapter_id = :chapterId
)";
        if (pages.Length == 0)
        {
            await _sql.Execute(DELETE_QUERY, new { id, chapterId, pages, platformId });
            return;
        }

        var pid = await _profile.Fetch(platformId);
        if (pid == null) return;

        await Upsert(new DbMangaBookmark
        {
            ProfileId = pid.Id,
            MangaId = id,
            MangaChapterId = chapterId,
            Pages = pages
        });
    }
}
