namespace MangaBox.Utilities.Comix;

/// <summary>
/// A service for bypassing Comix WAF (Web Application Firewall) protections (rotated images)
/// </summary>
public interface IComixWAFService
{
    /// <summary>
    /// Calculates the rotation angle between the target image and the rotated thumbnail image.
    /// </summary>
    /// <param name="waf">The Comix WAF generate parameters.</param>
    /// <returns>The rotation angle.</returns>
    int GetRotation(ComixWAFGenerate waf);

    /// <summary>
    /// Gets the result of a WAF solver
    /// </summary>
    /// <param name="waf">The parameters for the request</param>
    /// <param name="token">The cancellation token for the request</param>
    /// <returns>The result of the WAF request</returns>
    Task<ComixWAFVerify> GetCookie(ComixWAF waf, CancellationToken token);
}

internal class ComixWAFService(
    IApiService _api,
    ILogger<ComixWAFService> _logger) : IComixWAFService
{
    public const string DEBUG_DIR = "waf-test";
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public static bool DoDebugLog { get; set; } = false;

    public static Action<HttpRequestMessage> Message(ComixWAF waf)
    {
        var url = waf.Url.TrimStart("https://comix.to").ToString();
        var encode = Uri.EscapeDataString(url);
        url = $"https://comix.to/@waf/challenge?return=" + encode;

        var verFirst = waf.ChromeVersion.Split('.').First();

        var headers = DiExtensions.ComixBaseHeaders(url, waf.UserAgent);
        headers.Add("cookie", [$"cf_clearance={waf.CfClearance}"]);

        return c =>
        {
            foreach (var (key, value) in headers)
                c.Headers.Add(key, value);
        };
    }

    public static Action<IHttpBuilder> Request(ComixWAF waf)
    {
        return (c) => c.Message(Message(waf));
    }

    public async Task DebugLog<T>(string tag, T result, CancellationToken token)
    {
        const string HTML = """
            <!DOCTYPE html>
            <html>
            <body>
                <img alt="target" src="{0}" />
                <img alt="thumb" src="{1}" />
            </body>
            </html>
            """;

        if (!DoDebugLog) return;

        if (!Directory.Exists(DEBUG_DIR))
            Directory.CreateDirectory(DEBUG_DIR);

        using var io = File.Create(Path.Combine(DEBUG_DIR, $"{tag}.json"));
        await JsonSerializer.SerializeAsync(io, result, _options, token);
        await io.FlushAsync(token);

        if (result is not ComixWAFGenerate waf) return;

        using var ioHtml = File.Create(Path.Combine(DEBUG_DIR, $"{tag}.html"));
        var html = string.Format(HTML, waf.Image, waf.Thumb);
        using var writer = new StreamWriter(ioHtml);
        await writer.WriteAsync(html);
        await ioHtml.FlushAsync(token);
    }

    public async Task<ComixWAFVerify> GetCookie(ComixWAF waf, CancellationToken token)
    {
        var generate = await Generate(waf, token);
        if (generate is null)
            return new(false) { Content = "Failed to generate WAF parameters" };
        await DebugLog("generate", generate, token);

        var rotation = GetRotation(generate);
        var result = await Verify(waf, generate.CaptchaId, rotation, token);
        await DebugLog("verify", result, token);
        return result;
    }

    public Task<ComixWAFGenerate?> Generate(ComixWAF waf, CancellationToken token)
    {
        const string URL = "https://comix.to/@waf/generate";
        return _api.Get<ComixWAFGenerate>(URL, Request(waf), token: token);
    }

    public async Task<ComixWAFVerify> Verify(ComixWAF waf, string id, int angle, CancellationToken token)
    {
        try
        {
            var request = new ComixWAFVerifyRequest(id, angle);
            var result = await _api.Create("https://comix.to/@waf/verify", null, "POST", token)
                .Body(request)
                .Message(Message(waf))
                .Result();

            if (result is null)
            {
                _logger.LogWarning("Comix WAF verify return no response");
                return new(false) 
                {  
                    Content = "No response",
                    CaptchaId = id,
                    Rotation = angle,
                };
            }

            var content = await result.Content.ReadAsStringAsync(token);
            if (!result.IsSuccessStatusCode)
            {
                var code = result?.StatusCode ?? HttpStatusCode.InternalServerError;
                return new(false) 
                { 
                    Content = $"Code: {code}\r\nContent:\r\n{content}",
                    CaptchaId = id,
                    Rotation = angle,
                };
            }

            var verify = JsonSerializer.Deserialize<ComixWAFVerify>(content);
            if (verify is null)
                return new(false) 
                { 
                    Content = $"Code: WAF Failed\r\nContent:\r\n{content}",
                    CaptchaId = id,
                    Rotation = angle,
                };

            if (!verify.Success)
                return verify with 
                { 
                    Content = content,
                    CaptchaId = id,
                    Rotation = angle,
                };

            string[] cookie = result.Headers.TryGetValues("Set-Cookie", out var values) ? [..values] : [];

            var ttl = cookie.SelectMany(c => c.Split(';'))
                .Select(c => c.Trim())
                .Where(c => c.StartsWithIc("Max-Age="))
                .Select(c => c["Max-Age=".Length..])
                .Select(c => int.TryParse(c, out var v) ? v : 0)
                .FirstOrDefault();

            var pass = cookie.SelectMany(c => c.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .FirstOrDefault(t => t.StartsWithIc("waf_pass"));

            return verify with 
            { 
                Cookie = pass,
                TTL = ttl,
                Content = content,
                CaptchaId = id,
                Rotation = angle,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while verifying Comix WAF");
            return new(false) { Content = ex.ToString() };
        }
    }

    public int GetRotation(ComixWAFGenerate waf)
    {
        ArgumentNullException.ThrowIfNull(waf, nameof(waf));
        ArgumentException.ThrowIfNullOrWhiteSpace(waf.Image, nameof(waf.Image));
        ArgumentException.ThrowIfNullOrWhiteSpace(waf.Thumb, nameof(waf.Thumb));

        using var target = CenterImageRotationMatcher.LoadDataUri(waf.Image);
        using var rotate = CenterImageRotationMatcher.LoadDataUri(waf.Thumb);

        int size = waf.ThumbSize ?? 150;
        using var rotateThumb = CenterImageRotationMatcher.Resize(rotate, size);

        var rotation = (int)Math.Round(CenterImageRotationMatcher.FindRotation(target, rotateThumb), 0);
        return rotation < 0
            ? 360 + rotation
            : rotation;
    }

    public record class ComixWAFVerifyRequest(
        [property: JsonPropertyName("captcha_id")] string CaptchaId,
        [property: JsonPropertyName("angle")] int Angle);
}

/// <summary>
/// The data needed for the WAF Challenge
/// </summary>
/// <param name="Url">The URL you're trying to get to</param>
/// <param name="UserAgent">The User-Agent for the request</param>
/// <param name="CfClearance">The CF Clearance cookie</param>
public record class ComixWAF(
    string Url,
    string UserAgent,
    string CfClearance)
{
    /// <summary>
    /// The operating system being used
    /// </summary>
    public string OS => UserAgent.ContainsIc("Linux") ? "Linux" : "Windows";

    /// <summary>
    /// The version of chrome being used
    /// </summary>
    public string ChromeVersion => UserAgent.ContainsIc("Chrome/") ? UserAgent.Split("Chrome/")[1].Split(' ')[0] : "Unknown";
}

/// <summary>
/// Represents the response from the Comix WAF generate endpoint.
/// </summary>
/// <param name="CaptchaId">The ID of the CAPTCHA.</param>
/// <param name="Image">The URI base64 encoded image data.</param>
/// <param name="Thumb">The URI base64 encoded thumbnail data.</param>
/// <param name="Count">The count of images.</param>
/// <param name="ThumbSize">The size of the thumbnail.</param>
public record class ComixWAFGenerate(
    [property: JsonPropertyName("captcha_id")] string CaptchaId,
    [property: JsonPropertyName("image_base64")] string Image,
    [property: JsonPropertyName("thumb_base64")] string Thumb,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("thumb_size")] int? ThumbSize);

/// <summary>
/// Represents the response from the Comix WAF verify endpoint.
/// </summary>
/// <param name="Success">Whether the verification was successful.</param>
public record class ComixWAFVerify(
    [property: JsonPropertyName("success")] bool Success)
{
    /// <summary>
    /// The cookie returned from the Comix WAF verify endpoint, if any.
    /// </summary>
    public string? Cookie { get; set; }

    /// <summary>
    /// The time to live for the cookie
    /// </summary>
    public int TTL { get; set; }

    /// <summary>
    /// The content of the repsonse from the Comix WAF verify endpoint, if any.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The rotation detected
    /// </summary>
    public int Rotation { get; set; }

    /// <summary>
    /// The ID of the captcha that was verified.
    /// </summary>
    public string? CaptchaId { get; set; }

    /// <summary>
    /// The date the captcha was created.
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// The expiration date of the captcha.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset Expires => Created.AddSeconds(TTL);

    /// <summary>
    /// Whether or not the captcha is valid.
    /// </summary>
    [JsonIgnore]
    public bool Valid => Success && !string.IsNullOrWhiteSpace(Cookie) && Expires > DateTimeOffset.Now;
}