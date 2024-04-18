namespace MangaBox.Database.Services;

using Models;

public interface IProviderDbService : IOrmMap<Provider>
{

}

internal class ProviderDbService(IOrmService orm) : Orm<Provider>(orm), IProviderDbService
{

}
