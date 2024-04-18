namespace MangaBox.Database.Services;

using Models;

public interface IPageDbService : IOrmMap<Page>
{

}

internal class PageDbService(IOrmService orm) : Orm<Page>(orm), IPageDbService
{

}
