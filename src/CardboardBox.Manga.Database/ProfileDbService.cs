namespace CardboardBox.Manga.Database;

using Base;

public interface IProfileDbService
{
    Task<DbProfile?> Fetch(long id);

    Task<DbProfile?> Fetch(string? platformId);
}

public class ProfileDbService : OrmMap<DbProfile>, IProfileDbService
{
    private static string? _fetchByPlatformId;

    public ProfileDbService(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts) : base(query, sql, fakeUpserts) { }

    public Task<DbProfile?> Fetch(string? platformId)
    {
        if (string.IsNullOrEmpty(platformId)) return Task.FromResult<DbProfile?>(null);

        _fetchByPlatformId ??= _query.Select<DbProfile>(t => t.With(a => a.PlatformId));
        return _sql.Fetch<DbProfile>(_fetchByPlatformId, new { PlatformId = platformId });
    }
}
