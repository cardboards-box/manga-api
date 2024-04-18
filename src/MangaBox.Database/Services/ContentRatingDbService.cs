namespace MangaBox.Database.Services;

using Models;

public interface IContentRatingDbService : IOrmMap<ContentRating>
{

}

internal class ContentRatingDbService(IOrmService orm) : Orm<ContentRating>(orm), IContentRatingDbService
{

}
