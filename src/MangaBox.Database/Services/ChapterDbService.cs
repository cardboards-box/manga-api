namespace MangaBox.Database.Services;

using Models;

public interface IChapterDbService : IOrmMap<Chapter>
{

}

internal class ChapterDbService(IOrmService orm) : Orm<Chapter>(orm), IChapterDbService
{

}
