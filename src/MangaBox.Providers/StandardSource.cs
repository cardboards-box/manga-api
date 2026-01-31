namespace MangaBox.Providers;

using Models;

internal abstract class StandardSource(
    IApiService _api,
    IConfiguration _config) : IMangaSource
{
    private const string FALLBACK_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 OPR/106.0.0.0";

    public IApiService Api => _api;

    public IConfiguration Config => _config;

    public virtual int Priority => 1;

    public virtual string? UserAgent => Config["DefaultUserAgent"];

    public abstract string ProviderName { get; }

    public virtual string[] URLs { get; } = [];

    public virtual async Task<FileMemoryResponse?> GetImage(Image image, Provider provider)
    {
        var io = new MemoryStream();
        var (stream, size, file, type) = await Api.GetData(image.Url, c =>
        {
            if (string.IsNullOrEmpty(provider.Referrer)) return;

            c.Headers.Add("Referer", provider.Referrer);
            c.Headers.Add("Sec-Fetch-Dest", "document");
            c.Headers.Add("Sec-Fetch-Mode", "navigate");
            c.Headers.Add("Sec-Fetch-Site", "cross-site");
            c.Headers.Add("Sec-Fetch-User", "?1");
        }, UserAgent ?? FALLBACK_UA);
        await stream.CopyToAsync(io);
        io.Position = 0;

        return new FileMemoryResponse(io, size, file, type);
    }

    public virtual bool IsMatch(string url, Provider provider)
    {
        if (URLs.Length == 0)
            return url.ContainsIc(provider.Url);

        return URLs.Any(url.ContainsIc);
    }

    public abstract Task<Boxed> Load(string url, Provider provider);

    public abstract Task<Boxed> Update(Series series, Provider provider);
}
