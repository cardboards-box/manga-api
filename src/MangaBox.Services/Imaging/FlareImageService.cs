using Microsoft.Extensions.Caching.Memory;

namespace MangaBox.Services.Imaging;

using Utilities.Flare;
using Utilities.Flare.Models;

using Headers = Dictionary<string, string>;

/// <summary>
/// A service for fetching images with FlareSolverr
/// </summary>
public interface IFlareImageService
{
	/// <summary>
	/// Attempts to download the image using FlareSolverr
	/// </summary>
	/// <param name="url">The URL to download</param>
	/// <param name="headers">Any headers to use for the request</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the download attempt</returns>
	Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token);
}

internal class FlareImageService(
	IHttpService _http,
	IFlareSolverService _flare) : IFlareImageService
{
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());

	public FlareSolverInstance GetInstance(string url)
	{
		var key = new Uri(url).Host;
		return _cache.GetOrCreate(key, entry =>
		{
			var instance = _flare.Limiter();
			entry.Value = instance;
			entry.SlidingExpiration = TimeSpan.FromMinutes(1);
			entry.AbsoluteExpiration = DateTimeOffset.Now.Add(TimeSpan.FromMinutes(5));
			return instance;
		}) ?? _flare.Limiter();
	}

	public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
	{
		headers ??= [];

		var uri = new Uri(url);
		var instance = GetInstance(url);
		SolverCookie[] cookies = [..instance.Cookies.ToArray()];
		string? userAgent = instance.UserAgent;
		if (cookies.Length == 0 || string.IsNullOrEmpty(userAgent))
		{
			var result = await instance.GetHtml(url, token);
			cookies = result.FlareSolution.Cookies;
			userAgent = result.FlareSolution.UserAgent;
		}
		
		var cookie = CookieHeaderBuilder.BuildCookieHeader(cookies, uri);

		if (!string.IsNullOrEmpty(cookie))
			headers["cookie"] = cookie;
		if (!string.IsNullOrEmpty(userAgent))
			headers["user-agent"] = userAgent;

		return await _http.Download(url, headers, token);
	}
}
