namespace MangaBox.Database.Services;

using Models;

public interface IProfileDbService : IOrmMap<Profile>
{

}

internal class ProfileDbService(IOrmService orm) : Orm<Profile>(orm), IProfileDbService
{

}
