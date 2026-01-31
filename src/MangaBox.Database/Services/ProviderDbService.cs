namespace MangaBox.Database.Services;

using Models;

public interface IProviderDbService : IOrmMap<Provider>
{
    Task<Provider?> ByKey(string key);
}

internal class ProviderDbService(IOrmService orm) : CacheOrm<Provider>(orm), IProviderDbService
{
    public async Task<Provider?> ByKey(string key)
    {
        var all = await Get();
        return all.FirstOrDefault(p => p.Name.EqualsIc(key));
    }
}
