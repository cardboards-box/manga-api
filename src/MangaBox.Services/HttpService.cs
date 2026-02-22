using CardboardBox.Json;
using System.Net.Http.Headers;

namespace MangaBox.Services;

using Image = SixLabors.ImageSharp.Image;
using Headers = Dictionary<string, string>;

/// <summary>
/// A service for HTTP request helpers
/// </summary>
public interface IHttpService
{
	/// <summary>
	/// Generates the headers from the given items
	/// </summary>
	/// <param name="url">The URL of the thing being downloaded</param>
	/// <param name="source">The source of a manga</param>
	/// <param name="manga">The manga itself</param>
	/// <param name="image">The image</param>
	/// <returns>The generated headers</returns>
	Headers HeadersFrom(string url, MbSource? source, MbManga? manga, MbImage? image);

	/// <summary>
	/// Triggers the download
	/// </summary>
	/// <param name="url">The URL to download</param>
	/// <param name="headers">The headers to include in the request</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The HTTP response message</returns>
	Task<HttpResponseMessage?> GetResponse(string url, Headers? headers, CancellationToken token);

	/// <summary>
	/// Download a file 
	/// </summary>
	/// <param name="url">The URL to download</param>
	/// <param name="headers">The headers of the request</param>
	/// <param name="token">The cancellation token</param>
	/// <returns>The result of the download</returns>
	Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token);

	/// <summary>
	/// Determines the file extension for the given MIME type
	/// </summary>
	/// <param name="mimeType">The MIME type to determine the extension for</param>
	/// <returns>The file extension associated with the MIME type, or a default if unknown</returns>
	string DetermineExtension(string? mimeType);

	/// <summary>
	/// Determines the mimetype for the given file name
	/// </summary>
	/// <param name="filename">The file name</param>
	/// <returns>The mime type</returns>
	string DetermineMimeType(string filename);

	/// <summary>
	/// Determine the size of the images in pixels
	/// </summary>
	/// <param name="path">The path to the image file</param>
	/// <returns>A tuple containing the width and height of the image, or null if it cannot be determined</returns>
	Task<(int? width, int? height)> DetermineImageSize(string path);
}

internal class HttpService(
	IApiService _api,
	IJsonService _json,
	ILogger<HttpService> _logger) : IHttpService
{
	public const string EXT_DAT = "dat";

	/// <inheritdoc />
	public Headers HeadersFrom(string url, MbSource? source, MbManga? manga, MbImage? image)
	{
		var headers = new Headers(StringComparer.InvariantCultureIgnoreCase);

		var ua = manga?.UserAgent ?? source?.UserAgent ?? 
			(url.ContainsIc("mangadex") ? "MangaBox" : Constants.USER_AGENT);
		if (!string.IsNullOrEmpty(ua))
			headers["User-Agent"] = ua;

		var referer = manga?.Referer ?? source?.Referer;
		if (!string.IsNullOrEmpty(referer))
			headers["Referer"] = referer;

		foreach (var header in source?.Headers ?? [])
			if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value))
				headers[header.Key] = header.Value;

		foreach (var header in image?.Headers ?? [])
			if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value))
				headers[header.Key] = header.Value;

		return headers;
	}

	/// <inheritdoc />
	public Task<HttpResponseMessage?> GetResponse(string url, Headers? headers, CancellationToken token)
	{
		headers ??= HeadersFrom(url, null, null, null);
		var request = _api.Create(url, _json, "GET");
		request.Message(c =>
		{
			foreach (var header in headers)
				c.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}).CancelWith(token);

		return request.Result();
	}

	/// <inheritdoc />
	public async Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token)
	{
		var disposables = new List<IDisposable>();
		var response = await GetResponse(url, headers, token);
		if (response is null) 
			return new(disposables, url, headers, "Image came back empty");

		if (!response.IsSuccessStatusCode)
		{
			var content = await response.Content.ReadAsStringAsync(token);
			_logger.LogWarning("Failed to download external image >> {URL} >> {Status}: {Content}",
				url, response.StatusCode, content);
			return new(disposables, url, headers, "Failed to download image: " + content, response);
		}

		var mimeType = MimeType(response.Content.Headers);
		var fileName = FileName(response.Content.Headers, url, mimeType);
		var length = response.Content.Headers.ContentLength;
		var stream = await response.Content.ReadAsStreamAsync(token);
		disposables.Add(stream);

		return new(disposables, url, headers, null, response, stream, fileName, mimeType, length);
	}

	/// <inheritdoc />
	public string DetermineExtension(string? mimeType)
	{
		if (string.IsNullOrEmpty(mimeType))
			return EXT_DAT;

		return MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault() ?? EXT_DAT;
	}

	/// <inheritdoc />
	public string DetermineMimeType(string filename)
	{
		return MimeTypes.TryGetMimeType(filename, out var mimeType) ? mimeType : "application/octet-stream";
	}

	/// <inheritdoc />
	public async Task<(int? width, int? height)> DetermineImageSize(string path)
	{
		try
		{
			using var image = await Image.LoadAsync(path);
			return (image.Width, image.Height);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to determine image size for >> {Path}", path);
			return (null, null);
		}
	}

	/// <summary>
	/// Gets the file name from the content disposition
	/// </summary>
	/// <param name="headers">The headers of the response</param>
	/// <param name="url">The URL of the image</param>
	/// <param name="mimeType">The MIME type of the image</param>
	/// <returns>The file name of the image</returns>
	public string? FileName(HttpContentHeaders? headers, string url, string? mimeType)
	{
		var path = headers?.ContentDisposition?.FileName
			?? headers?.ContentDisposition?.Parameters?.FirstOrDefault()?.Value;
		if (!string.IsNullOrEmpty(path)) return path;

		path = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Split('?').FirstOrDefault();
		if (!string.IsNullOrEmpty(path)) return path;

		var ext = DetermineExtension(mimeType);
		return $"file.{ext}";
	}

	/// <summary>
	/// Gets the mime-type from the content type
	/// </summary>
	/// <param name="headers">The content headers</param>
	/// <returns>The mime-type</returns>
	public static string MimeType(HttpContentHeaders? headers)
	{
		return headers?.ContentType?.MediaType
			?? headers?.ContentType?.ToString().Split(';').First()
			?? "application/octet-stream";
	}
}

/// <summary>
/// A download result that can be returned from the <see cref="HttpService"/>
/// </summary>
/// <param name="Url">The URL of the downloaded file</param>
/// <param name="Headers">The headers of the downloaded file</param>
/// <param name="Error">The error message, if any</param>
/// <param name="Response">The HTTP response message</param>
/// <param name="Stream">The stream of the downloaded file</param>
/// <param name="FileName">The name of the downloaded file</param>
/// <param name="MimeType">The MIME type of the downloaded file</param>
/// <param name="Disposables">The disposables associated with the download</param>
/// <param name="Length">The length of the content</param>
public record class DownloadResult(
	IEnumerable<IDisposable> Disposables,
	string Url,
	Headers? Headers = null,
	string? Error = null,
	HttpResponseMessage? Response = null,
	Stream? Stream = null,
	string? FileName = null,
	string? MimeType = null,
	long? Length = null) : IDisposable
{
	/// <inheritdoc />
	public void Dispose()
	{
		foreach (var disposable in Disposables)
			disposable.Dispose();
		GC.SuppressFinalize(this);
	}
}