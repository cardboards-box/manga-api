namespace MangaBox.Database.Services;

using Models;

public interface IVolumeDbService : IOrmMap<Volume>
{
    Task<Volume[]> BySeries(Guid id);
}

internal class VolumeDbService(IOrmService orm) : Orm<Volume>(orm), IVolumeDbService
{
    private static string? _bySeries;

    public Task<Volume[]> BySeries(Guid id)
    {
        _bySeries ??= Map.Select(t => t.With(a => a.SeriesId));
        return Get(_bySeries, new { SeriesId = id });
    }
}
