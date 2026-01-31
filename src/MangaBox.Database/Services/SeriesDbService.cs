namespace MangaBox.Database.Services;

using Models;

public interface ISeriesDbService : IOrmMap<Series>
{
    Task<Series?> ByPlatform(Guid provider, string sourceId);
}

internal class SeriesDbService(IOrmService orm) : Orm<Series>(orm), ISeriesDbService
{
    private static string? _byPlatform;

    public Task<Series?> ByPlatform(Guid provider, string sourceId)
    {
        _byPlatform ??= Map.Select(t => t.With(a => a.ProviderId).With(a => a.SourceId).Null(a => a.DeletedAt));
        return Fetch(_byPlatform, new { ProviderId = provider, SourceId = sourceId });
    }
}
