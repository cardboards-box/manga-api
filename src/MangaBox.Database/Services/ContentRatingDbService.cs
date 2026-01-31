namespace MangaBox.Database.Services;

using Models;

public interface IContentRatingDbService : IOrmMap<ContentRating>
{
    Task<ContentRating?> ByName(string name);
}

internal class ContentRatingDbService(IOrmService orm) : CacheOrm<ContentRating>(orm), IContentRatingDbService
{
    public async Task<ContentRating?> ByName(string name)
    {
        var all = await Get();
        return all.FirstOrDefault(r => r.Name.EqualsIc(name));
    }
}
