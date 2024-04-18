namespace MangaBox.Database.Services;

using Models;

public interface IRoleDbService : IOrmMap<Role>
{
    Task<Role[]> Fetch(Guid[] ids);

    Task<Role[]> Default();
}

internal class RoleDbService(IOrmService orm) : CacheOrm<Role>(orm), IRoleDbService
{
    public async Task<Role[]> Fetch(Guid[] ids)
    {
        var all = await Get();
        return all.Where(r => ids.Contains(r.Id)).ToArray();
    }

    public async Task<Role[]> Default()
    {
        return (await Get())
            .Where(r => r.Name == Role.USER)
            .ToArray();
    }
}
