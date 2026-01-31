namespace MangaBox.Providers;

public interface ISourceProviderService
{
    Task<SourceProvider?> ById(Guid id);

    Task<SourceProvider?> ByName(string name);

    Task<SourceProvider?> ByUrl(string url);

    IAsyncEnumerable<SourceProvider> SourceProviders();
}

internal class SourceProviderService(
    IDbService _db,
    IServiceProvider _services) : ISourceProviderService
{
    private IMangaSource[]? _sources;

    public IMangaSource[] Sources => _sources ??=
    [..
        _services
            .GetServices<IMangaSource>()
            .OrderByDescending(x => x.Priority)
    ];

    public IMangaSource? SourceByName(string name)
    {
        return Sources.FirstOrDefault(x => x.ProviderName.EqualsIc(name));
    }

    public async IAsyncEnumerable<SourceProvider> SourceProviders()
    {
        var providers = await _db.Providers.Get();
        foreach(var provider in providers)
        {
            var source = SourceByName(provider.Name);
            if (source is null)
                continue;

            yield return new SourceProvider(source, provider);
        }
    }

    public async Task<SourceProvider?> ById(Guid id)
    {
        return await SourceProviders()
            .FirstOrDefaultAsync(x => x.Provider.Id == id);
    }

    public async Task<SourceProvider?> ByUrl(string url)
    {
        return await SourceProviders()
            .FirstOrDefaultAsync(x => x.Source.IsMatch(url, x.Provider));
    }

    public async Task<SourceProvider?> ByName(string name)
    {
        return await SourceProviders()
            .FirstOrDefaultAsync(x => x.Provider.Name.EqualsIc(name));
    }
}
