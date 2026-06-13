using SixLabors.ImageSharp;
using System.Threading.RateLimiting;

namespace MangaBox.Providers;

using Models.Types;
using Services.Imaging;

/// <summary>
/// The base class for manga sources.
/// </summary>
public abstract class BaseMangaSource<T> : IMangaSource
	where T : class, IMangaSource
{
	internal static RateLimiter? _limiter;

	/// <inheritdoc />
	public abstract string HomeUrl { get; }

	/// <inheritdoc />
	public abstract string Provider { get; }

	/// <inheritdoc />
	public abstract string Name { get; }

	/// <inheritdoc />
	public virtual string? Referer => HomeUrl;

	/// <inheritdoc />
	public virtual string? UserAgent => PolyfillExtensions.USER_AGENT;

	/// <inheritdoc />
	public virtual bool Enabled => true;

	/// <inheritdoc />
	public virtual bool Hidden => false;

	/// <inheritdoc />
	public virtual Dictionary<string, string>? Headers => Referer?.ForceNull() is not null ? PolyfillExtensions.HEADERS_FOR_REFERS : null;

	/// <inheritdoc />
	public virtual bool UseFlareImages => false;

	/// <inheritdoc />
	public virtual bool UseFlareImagesCover => false;

	/// <inheritdoc />
	public virtual bool UseProxiedImages => false;

	/// <inheritdoc />
	public virtual ContentRating? DefaultRating => null;

	/// <inheritdoc />
	public virtual TimeSpan IndexFrequency => TimeSpan.Zero;

	/// <inheritdoc />
	public virtual bool IndexEnabled => IndexFrequency > TimeSpan.Zero;

	/// <inheritdoc />
	public virtual RateLimiter GetRateLimiter(string url) => _limiter ??= PolyfillExtensions.DefaultRateLimiter();

	/// <inheritdoc />
	public virtual Task<Image?> PostProcessing(DownloadResult result, Image? image, CancellationToken token) => Task.FromResult(image);

	/// <inheritdoc />
	public virtual Task PostProcessDownload(DownloadResult result, string path, CancellationToken token) => Task.CompletedTask;

	/// <inheritdoc />
	public virtual IAsyncEnumerable<ImportManga> Index(LoaderSource source, CancellationToken token) => AsyncEnumerable.Empty<ImportManga>();

	/// <inheritdoc />
	public abstract Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token);

	/// <inheritdoc />
	public abstract Task<ImportManga?> Manga(string id, CancellationToken token);

	/// <inheritdoc />
	public abstract (bool matches, string? part) MatchesProvider(string url);

}
