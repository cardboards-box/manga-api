using MangaBox.Models;

namespace MangaBox.Providers;

public interface IImportService
{
    Task<Boxed> Load(string url);

    Task<Boxed> Update(Series series);

    Task<FileMemoryResponse?> Image(Image image);
}

internal class ImportService(
    IDbService _db,
    ISourceProviderService _sources) : IImportService
{
    public async Task<Boxed> Load(string url)
    {
        var provider = await _sources.ByUrl(url);
        if (provider is null)
            return Boxed.Bad("No source loader found for url.");

        return await provider.Source.Load(url, provider.Provider);
    }

    public async Task<Boxed> Update(Series series)
    {
        var provider = await _sources.ById(series.ProviderId);
        if (provider is null)
            return Boxed.Bad("No source loader found for series.");

        return await provider.Source.Update(series, provider.Provider);
    }

    public async Task<FileMemoryResponse?> Image(Image image)
    {
        var provider = await _sources.ById(image.ProviderId);
        if (provider is null)
            return null;

        return await provider.Source.GetImage(image, provider.Provider);
    }
}
