namespace MangaBox.Database.Services;

using Models;

public interface ISeriesDbService : IOrmMap<Series>
{

}

internal class SeriesDbService(IOrmService orm) : Orm<Series>(orm), ISeriesDbService
{

}
