namespace MangaBox.Utilities.Comix;

using Flare;
using Flare.Models;
using MangaBox.Services.Imaging;

/// <summary>
/// A service for getting HTML documents from Comix
/// </summary>
public interface IComixHtmlService : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Fetches HTML from Comix
    /// </summary>
    /// <param name="url">The URL of the document to request</param>
    /// <param name="token">The cancellation token</param>
    /// <returns>The returned HTML</returns>
    Task<FlareHtmlDocument> GetHtml(string url, CancellationToken token);
}

internal class ComixHtmlService(
    IApiService _api,
    IComixWAFService _waf,
    IFlareSolverService _flare,
    IProxiedHttpService _proxies,
    ILogger<ComixHtmlService> _logger) : IComixHtmlService
{
    private const int WAF_RETRY_MAX = 3;
    private const int WAF_RETRY_MIN_WAIT = 10;
    private const int WAF_RETRY_MAX_WAIT = 30;
    private const string DEBUG_DIR = "comix-html";

    public static bool DEBUG { get; set; } = true;

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
    };

    private ComixWAFVerify? _wafResult;
    private SolverSolution? _flareResult;
    private FlareSolverInstance? _instance;
    private SolverSession? _session;
    private ProxyEndpoint? _proxyEndpoint;
    private readonly SemaphoreSlim _instanceLock = new(1, 1);

    private SolverCookie? _flareCookie => _flareResult?.Cookies.FirstOrDefault(t => t.Name.EqualsIc("cf_clearance"));

    public static async Task Debug(string tag, CancellationToken token, params object?[] states)
    {
        if (!DEBUG) return;

        if (!Directory.Exists(DEBUG_DIR))
            Directory.CreateDirectory(DEBUG_DIR);

        var path = Path.Combine(DEBUG_DIR, $"{DateTimeOffset.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}_{tag}");

        for(var i = 0; i < states.Length; i++)
        {
            var state = states[i];
            if (state is null) continue;

            if (state is FlareHtmlDocument doc)
            {
                await File.WriteAllTextAsync($"{path}-state-{i + 1}.html", doc.FlareSolution.Response, token);
                state = doc.FlareSolution;
            }

            var result = JsonSerializer.Serialize(state, state.GetType(), _options);
            await File.WriteAllTextAsync($"{path}-state-{i + 1}.json", result, token);
        }
    }

    public async Task<FlareSolverInstance> Instance(CancellationToken token)
    {
        if (_instance is not null)
            return _instance;

        await _instanceLock.WaitAsync(token);
        try
        {
            if (_instance is not null)
                return _instance;

            var (endpoint, lease) = await _proxies.Aquire(token);
            using (lease)
            {
                _logger.LogInformation("Creating Comix FlareSolverr session through proxy {ProxyUrl}", endpoint.Url);
                _session = await _flare.CreateSession(endpoint.CreateSolverProxy(), token);
            }
            _proxyEndpoint = endpoint;

            _instance = new FlareSolverInstance(_session, _logger)
            {
                MaxRequestsBeforePauseMin = 5,
                MaxRequestsBeforePauseMax = 15,
                ResponseWait = TimeSpan.FromSeconds(2),
                DisableMedia = false
            };
            return _instance;
        }
        finally
        {
            _instanceLock.Release();
        }
    }

    public async Task<bool> GetWaf(string url, CancellationToken token)
    {
        if (_wafResult is not null && _wafResult.Valid)
            return true;

        if (_flareResult is null || _flareCookie is null)
            return false;

        for (var i = 0; i < WAF_RETRY_MAX; i++)
        {
            _wafResult = await _waf.GetCookie(
                new(url, _flareResult.UserAgent, _flareCookie.Value),
                _proxyEndpoint,
                token);
            await Debug(nameof(GetWaf), token, _wafResult);
            if (_wafResult.Success)
                return true;

            var wait = Random.Shared.Next(WAF_RETRY_MIN_WAIT, WAF_RETRY_MAX_WAIT);
            _logger.LogInformation("WAF Fetch failed, retrying in {Wait} seconds (attempt {Attempt}/{MaxAttempts}): {State}",
                wait, i + 1, WAF_RETRY_MAX, JsonSerializer.Serialize(_wafResult, _options));
            await Task.Delay(TimeSpan.FromSeconds(wait), token);
        }

        return false;
    }

    public bool CanRawDog([MaybeNullWhen(false)] out string cf, [MaybeNullWhen(false)] out string waf, [MaybeNullWhen(false)] out string ua)
    {
        cf = waf = ua = null;

        _ = Debug(nameof(CanRawDog), 
            default, 
            _flareResult, 
            _wafResult,
            _wafResult?.Valid ?? false,
            (_flareResult?.Cookies
                .FirstOrDefault(t => t.Name.EqualsIc("cf_clearance"))?
                .Expires.EpochSeconds() ?? DateTime.MinValue).ToString("yyyy-MM-dd_HH-mm-ss-fff"));

        if (_flareResult is null ||
            _wafResult is null ||
            !_wafResult.Valid ||
            string.IsNullOrEmpty(_wafResult.Cookie))
            return false;

        var cfCookie = _flareResult.Cookies.FirstOrDefault(t => t.Name.EqualsIc("cf_clearance"));
        if (cfCookie is null) return false;

        var expires = cfCookie.Expires.EpochSeconds();
        if (expires < DateTimeOffset.UtcNow)
            return false;

        cf = cfCookie.Value;
        waf = _wafResult.Cookie;
        ua = _flareResult.UserAgent;
        return true;
    }

    public async Task<FlareHtmlDocument?> RawDog(string url, CancellationToken token)
    {
        if (!CanRawDog(out var cf, out var waf, out var ua))
            return null;

        var headers = DiExtensions.ComixBaseHeaders(url, ua);
        headers.Add("cookie", [$"cf_clearance={cf}; {waf}"]);

        var request = _api.Create(url, null, "GET", token: token)
            .Message(c =>
            {
                foreach (var (key, value) in headers)
                    c.Headers.Add(key, value);
            });
        if (_proxyEndpoint is not null)
            request.ClientFactory(_ => _proxyEndpoint.CreateClient());

        var result = await request.Result();
        if (result is null) return null;

        var content = await result.Content.ReadAsStringAsync(token);
        var doc = new FlareHtmlDocument()
        { 
            FlareSolution = _flareResult! 
        };
        doc.LoadHtml(content);
        await Debug(nameof(RawDog), token, doc, _wafResult, headers);
        return doc;
    }

    public async Task<FlareHtmlDocument> Flared(string url, CancellationToken token)
    {
        var instance = await Instance(token);
        if (_wafResult is not null &&
            _wafResult.Valid &&
            !string.IsNullOrWhiteSpace(_wafResult.Cookie))
        {
            var parts = _wafResult.Cookie.Split('=', 2);
            if (parts.Length == 2 && parts[0].EqualsIc("waf_pass"))
            {
                instance.SetCookie(parts[0], parts[1]);
                var cookie = instance.Cookies.First(t => t.Name.EqualsIc(parts[0]));
                cookie.Domain = ".comix.to";
                cookie.Path = "/";
                cookie.Secure = true;
                cookie.HttpOnly = true;
                cookie.SameSite = "Lax";
            }
        }

        // FlareSolverr ignores request-level proxies when a session is supplied. The
        // proxy is therefore bound when the session is created in Instance().
        var result = await instance.GetHtml(url, token);
        if (IsBrowserError(result, out var error))
            throw new HttpRequestException($"FlareSolverr browser failed to load {url}: {error}");

        _flareResult = result.FlareSolution;
        await Debug(nameof(Flared), token, result);
        return result;
    }

    private static bool IsSecurityCheck(FlareHtmlDocument document)
    {
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty;
        return title.ContainsIc("Security check");
    }

    private static bool IsBrowserError(FlareHtmlDocument document, [MaybeNullWhen(false)] out string error)
    {
        error = document.DocumentNode
            .SelectSingleNode("//*[@id='error-debugging-info']")?
            .InnerText?
            .Trim();

        if (!string.IsNullOrWhiteSpace(error))
            return true;

        var response = document.FlareSolution.Response;
        var match = Regex.Match(response, @"&quot;errorCode&quot;:&quot;(?<code>ERR_[A-Z0-9_]+)&quot;");
        if (!match.Success)
            match = Regex.Match(response, "\\\"errorCode\\\"\\s*:\\s*\\\"(?<code>ERR_[A-Z0-9_]+)\\\"");

        error = match.Success ? match.Groups["code"].Value : null;
        return error is not null;
    }

    public async Task<FlareHtmlDocument> GetHtml(string url, CancellationToken token)
    {
        var result = await Flared(url, token);
        if (!IsSecurityCheck(result) || !await GetWaf(url, token))
            return result;

        return await Flared(url, token);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        _wafResult = null;
        _flareResult = null;
        _instance = null;
        _proxyEndpoint = null;
        await (_session?.DisposeAsync() ?? ValueTask.CompletedTask);
        _session = null;
    }
}
