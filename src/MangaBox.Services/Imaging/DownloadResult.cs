namespace MangaBox.Services.Imaging;

using Headers = Dictionary<string, string>;

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