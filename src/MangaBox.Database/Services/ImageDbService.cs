namespace MangaBox.Database.Services;

using Models;

public interface IImageDbService : IOrmMap<Image>
{
    Task<Image?> Cover(Guid seriesId);

    Task<Image?> Page(Guid pageId);
}

internal class ImageDbService(IOrmService orm) : Orm<Image>(orm), IImageDbService
{
    public Task<Image?> Cover(Guid seriesId)
    {
        const string QUERY = @"SELECT i.*
FROM mb_images i
JOIN mb_series s ON s.cover_id = i.id
WHERE 
    s.id = :seriesId AND
    s.deleted_at IS NULL AND
    i.deleted_at IS NULL";

        return Fetch(QUERY, new { seriesId });
    }

    public Task<Image?> Page(Guid pageId)
    {
        const string QUERY = @"SELECT i.*
FROM mb_images i
JOIN mb_pages p ON p.image_id = i.id
WHERE
    p.id = :pageId AND
    p.deleted_at IS NULL AND
    i.deleted_at IS NULL";
        return Fetch(QUERY, new { pageId });
    }
}
