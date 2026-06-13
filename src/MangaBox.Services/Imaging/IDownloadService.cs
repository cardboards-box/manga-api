namespace MangaBox.Services.Imaging;


using Headers = Dictionary<string, string>;

/// <summary>
/// Represents a service that downloads files
/// </summary>
public interface IDownloadService
{
	/// <summary>
	/// Attempts to download the given file
	/// </summary>
	/// <param name="url">The URL to download</param>
	/// <param name="headers">Any headers to use for the request</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the download attempt</returns>
	Task<DownloadResult> Download(string url, Headers? headers, CancellationToken token);
}
