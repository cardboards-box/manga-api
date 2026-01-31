namespace MangaBox.Database.Services;

using Models;

public interface ITagDbService : IOrmMap<Tag>
{

}

internal class TagDbService(IOrmService orm) : CacheOrm<Tag>(orm), ITagDbService
{

}
