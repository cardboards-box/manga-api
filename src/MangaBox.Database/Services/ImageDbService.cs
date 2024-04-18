namespace MangaBox.Database.Services;

using Models;

public interface IImageDbService : IOrmMap<Image>
{

}

internal class ImageDbService(IOrmService orm) : Orm<Image>(orm), IImageDbService
{

}
