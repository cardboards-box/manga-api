namespace MangaBox.Database.Services;

using Models;

public interface ISeriesPeopleDbService : IOrmMap<SeriesPeople>
{

}

internal class SeriesPeopleDbService(IOrmService orm) : Orm<SeriesPeople>(orm), ISeriesPeopleDbService
{

}
