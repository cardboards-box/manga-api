namespace MangaBox.Database.Services;

using Models;

public interface IPersonDbService : IOrmMap<Person>
{

}

internal class PersonDbService(IOrmService orm) : Orm<Person>(orm), IPersonDbService
{

}
