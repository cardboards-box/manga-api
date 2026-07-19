namespace MangaBox.Services.Imaging;

using Utilities.Flare;
using Utilities.Flare.Models;

using Headers = Dictionary<string, string>;

/// <summary>
/// A service for fetching images through a proxy and FlareSolverr
/// </summary>
public interface IProxiedFlareImageService : IDownloadService { }

internal class ProxiedFlareImageService(
    IHttpService _http,
    IProxiedHttpService _proxy,
    IFlareImageService _flare,
    ILogger<ProxiedFlareImageService> _logger) : IProxiedFlareImageService
{
#if DEBUG
    public static bool Debug { get; set; } = true;
#else
    public static bool Debug { get; set; } = false;
#endif

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task DebugLog(string url, FlareHtmlDocument doc, SolverCookie[] cookies, string? userAgent, CancellationToken token)
    {
        const string OUTPUT_DIR = "flare-proxy-debug";

        if (!Directory.Exists(OUTPUT_DIR))
        {
            Directory.CreateDirectory(OUTPUT_DIR);
            _logger.LogInformation("Created debug output directory: {OutputDir}", OUTPUT_DIR);
        }

        var hash = url.MD5Hash();

        using var jio = File.Create(Path.Combine(OUTPUT_DIR, $"{hash}.json"));
        await JsonSerializer.SerializeAsync(jio, new DebugData(url, cookies, userAgent, 
            doc.FlareSolution.Status, 
            doc.FlareSolution.Cookies, 
            doc.FlareSolution.UserAgent,
            doc.FlareSolution.Headers, 
            doc.FlareSolution.TurnstileToken), _options, token);
        await jio.FlushAsync(token);
        await jio.DisposeAsync();


        using var fio = File.Create(Path.Combine(OUTPUT_DIR, $"{hash}.html"));
        doc.Save(fio);
        await fio.FlushAsync(token);
        await fio.DisposeAsync();

        _logger.LogInformation("Wrote debug data for {Url} to {OutputDir}/{Hash}.json and .html", url, OUTPUT_DIR, hash);
    }

    public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
    {
        var (endpoint, lease) = await _proxy.Aquire(token);
        using var _ = lease;
        _logger.LogDebug("Downloading {Url} through a proxied flare solver instance: {ProxyUrl}", url, endpoint.Url);

        headers ??= [];
        var uri = new Uri(url);
        var instance = _flare.GetInstance(url);
        SolverCookie[] cookies = [.. instance.Cookies.ToArray()];
        string? userAgent = instance.UserAgent;

        if (cookies.Length == 0 || string.IsNullOrEmpty(userAgent))
        {
            var proxy = new SolverProxy { Url = endpoint.Url, };
            var result = await instance.GetHtml(url, token, proxy: proxy);
            if (Debug) await DebugLog(url, result, cookies, userAgent, token);
            cookies = result.FlareSolution.Cookies;
            userAgent = result.FlareSolution.UserAgent;
        }

        var cookie = CookieHeaderBuilder.BuildCookieHeader(cookies, uri);

        if (!string.IsNullOrEmpty(cookie))
            headers["cookie"] = cookie;
        if (!string.IsNullOrEmpty(userAgent))
            headers["user-agent"] = userAgent;

        return await _http.Download(url, headers, request =>
        {
            request.ClientFactory(_ => endpoint.CreateClient());
        }, token);
    }

    internal record class DebugData(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("cookies")] SolverCookie[] Cookies,
        [property: JsonPropertyName("userAgent")] string? UserAgent,
        [property: JsonPropertyName("flareStatus")] int FlareStatus,
        [property: JsonPropertyName("flareCookies")] SolverCookie[] FlareCookies,
        [property: JsonPropertyName("flareUserAgent")] string? FlareUserAgent,
        [property: JsonPropertyName("flareHeaders")] Headers? FlareHeaders,
        [property: JsonPropertyName("flareToken")] string? FlareToken);
}
