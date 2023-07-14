using CardboardBox.Core;

namespace CardboardBox.Manga.ReverseImageSearch.MatchApi;

public interface IMatchService
{
    Task<MatchMeta<MangaMetadata>[]> Search(string image);
    Task<MatchMeta<MangaMetadata>[]> Search(Stream stream, string filename);
    Task<MatchMeta<MangaMetadata>[]> Search(MemoryStream stream, string filename);
    Task<bool> IndexPageProxy(string image, MangaMetadata metadata, string? referer, bool noCache = false);
    Task<bool> IndexPage(string url, MangaMetadata metadata);
}

public class MatchService : IMatchService
{
    private readonly ILogger _logger;
    private readonly IMatchApiService _api;

    public MatchService(
        ILogger<MatchService> logger, 
        IMatchApiService api)
    {
        _logger = logger;
        _api = api;
    }

    public async Task<MatchMeta<MangaMetadata>[]> Search(string image)
    {
        var url = MangaHelper.ProxyUrlExternal(image);
        var result = _api.Search<MangaMetadata>(url);
        return await Search(result, image);
    }

    public async Task<MatchMeta<MangaMetadata>[]> Search(Task<MatchSearchResults<MangaMetadata>?> task, string name)
    {
        var result = await task;
        if (result == null)
        {
            _logger.LogError($"Error occurred while searching for image: {name}");
            return Array.Empty<MatchMeta<MangaMetadata>>();
        }

        if (!result.Success)
        {
            _logger.LogError($"Error occurred while searching for image, {string.Join(", ", result.Error)}: {name}");
            return Array.Empty<MatchMeta<MangaMetadata>>();
        }

        return result.Result;
    }

    public Task<bool> IndexPageProxy(string image, MangaMetadata metadata, string? referer, bool noCache = false)
    {
        var imageUrl = metadata.Type == MangaMetadataType.Page 
            ? MangaHelper.ProxyUrlMangaPage(image, referer, noCache) 
            : MangaHelper.ProxyUrlMangaCover(image, referer, noCache);

        return IndexPage(imageUrl, metadata);
    }

    public async Task<bool> IndexPage(string url, MangaMetadata metadata)
    {
        var filename = GenerateId(metadata);
        var result = await _api.Add(url, filename, metadata);

        if (result == null)
        {
            _logger.LogError("Error occurred while indexing image, the result was null: {Id} >> {Source}", 
                metadata.Id, metadata.Source);
            return false;
        }

        if (!result.Success)
        {
            _logger.LogError("Error occurred while indexing image, {Error}: {Id} >> {Source}", 
                string.Join(", ", result.Error), metadata.Id, metadata.Source);
            return false;
        }

        return true;
    }

    public async Task<MatchMeta<MangaMetadata>[]> Search(Stream stream, string filename)
    {
        var result = _api.Search<MangaMetadata>(stream, filename);
        return await Search(result, filename);
    }

    public async Task<MatchMeta<MangaMetadata>[]> Search(MemoryStream stream, string filename)
    {
        var result = _api.Search<MangaMetadata>(stream, filename);
        return await Search(result, filename);
    }

    public static string GenerateId(MangaMetadata data)
    {
        return data.Type switch
        {
            MangaMetadataType.Page => $"page:{data.MangaId}:{data.ChapterId}:{data.Page}",
            MangaMetadataType.Cover => $"cover:{data.MangaId}:{data.Id}",
            _ => $"unknown:{data.Id}"
        };
    }
}
