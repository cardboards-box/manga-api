namespace MangaBox.Database.Services;

using Models;

public interface IVolumeDbService : IOrmMap<Volume>
{

}

internal class VolumeDbService(IOrmService orm) : Orm<Volume>(orm), IVolumeDbService
{

}
