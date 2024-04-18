namespace MangaBox.Database.Services;

using Models;

public interface IRequestLogDbService : IOrmMap<RequestLog>
{

}

internal class RequestLogDbService(IOrmService orm) : Orm<RequestLog>(orm), IRequestLogDbService
{

}
