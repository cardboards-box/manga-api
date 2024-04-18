namespace MangaBox.Database.Services;

using Models;

public interface ILoginDbService : IOrmMap<Login>
{
    Task<(Login? login, Profile? profile)> ByPlatform(string id);

    Task<Login[]> ByProfile(Guid id);
}

internal class LoginDbService(IOrmService orm) : Orm<Login>(orm), ILoginDbService
{
    private static string? _byProfile;

    public async Task<(Login? login, Profile? profile)> ByPlatform(string id)
    {
        const string QUERY = @"
SELECT 
    l.*, 
    '' as split, 
    p.*
FROM mb_logins l
JOIN mb_profiles p ON l.profile_id = p.id
WHERE
    l.platform_id = :id AND
    l.deleted_at IS NULL AND
    p.deleted_at IS NULL";
        
        var res = await _sql.QueryTupleAsync<Login, Profile>(QUERY, new { id });
        return res.Length == 0 ? (null, null) : res[0];
    }

    public Task<Login[]> ByProfile(Guid id)
    {
        _byProfile ??= Map.Select(t => t.With(a => a.ProfileId));
        return Get(_byProfile, new { ProfileId = id });
    }
}
