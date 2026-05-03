namespace MangaBox.Services.Imaging;

/// <summary>
/// Represents a resulting image from the image service
/// </summary>
/// <param name="Error">The error message if applicable</param>
/// <param name="Stream">The image stream</param>
/// <param name="Disposables">Any disposable items</param>
/// <param name="FromCache">Indicates if the image was retrieved from cache or the source</param>
/// <param name="Image">The image data</param>
/// <param name="OverrideOrdinal">The optional ordinal to override the one on the base image</param>
public record class ImageResult(
	string? Error,
	MbImage Image,
	Stream? Stream = null,
	bool FromCache = true,
	int? OverrideOrdinal = null,
	IDisposable[]? Disposables = null) : IDisposable
{
	private List<IDisposable>? _toBeDisposed;

	/// <summary>
	/// Anything to be disposed after the image is done being used.
	/// </summary>
	public List<IDisposable> ToBeDisposed
	{
		get
		{
			if (_toBeDisposed is not null) 
				return _toBeDisposed;

			_toBeDisposed = [];
			if (Stream is not null)
				_toBeDisposed.Add(Stream);
			if (Disposables is not null && Disposables.Length > 0)
				_toBeDisposed.AddRange(Disposables);

			return _toBeDisposed;
		}
	}

	/// <summary>
	/// The ordinal of the image in the set
	/// </summary>
	public int Ordinal => OverrideOrdinal ?? Image.Ordinal;

	/// <summary>
	/// The ID of the file
	/// </summary>
	public Guid FileId => Image.Id;

	/// <summary>
	/// The name of the file
	/// </summary>
	public string? FileName => Image?.FileName;

	/// <summary>
	/// The mime-type / content-type
	/// </summary>
	public string? MimeType => Image?.MimeType;

	/// <summary>
	/// The width of the image in pixels
	/// </summary>
	public int? Width => Image?.ImageWidth;

	/// <summary>
	/// The height of the image in pixels
	/// </summary>
	public int? Height => Image?.ImageHeight;

	/// <inheritdoc />
	public void Dispose()
	{
		ToBeDisposed.Each(t => t.Dispose());
		GC.SuppressFinalize(this);
	}
}
