namespace CardboardBox.Manga.ReverseImageSearch.NsfwCheck;

public interface INsfwApiService
{
    Task<NsfwResult?> Get(string url);
}

public class NsfwApiService : INsfwApiService
{
    private readonly IApiService _api;
    private readonly IConfiguration _config;

    public string ApiUrl => _config["Reverse:NsfwUrl"] ?? throw new NullReferenceException("Reverse:NsfwUrl Config is null");

    public NsfwApiService(
        IApiService api,
        IConfiguration config)
    {
        _api = api;
        _config = config;
    }

    public Task<NsfwResult?> Get(string url)
    {
        var uri = $"{ApiUrl}/nsfw/{WebUtility.UrlEncode(url)}";
        return _api.Get<NsfwResult?>(uri);
    }
}
