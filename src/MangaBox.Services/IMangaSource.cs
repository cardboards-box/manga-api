using System.Threading.RateLimiting;

namespace MangaBox.Services;

using Models.Composites.Import;
using Models.Types;

/// <summary>
/// Represents a service that provides access to a third party manga source
/// </summary>
public interface IMangaSource
{
	/// <summary>
	/// The home URL of the manga source
	/// </summary>
	string HomeUrl { get; }

	/// <summary>
	/// The provider slug for the source
	/// </summary>
	string Provider { get; }

	/// <summary>
	/// The display name of the manga source
	/// </summary>
	string Name { get; }

	/// <summary>
	/// The referer to add as a header when making image requests
	/// </summary>
	string? Referer { get; }

	/// <summary>
	/// The user-agent to add when making image requests
	/// </summary>
	string? UserAgent { get; }

	/// <summary>
	/// The headers to add when making image requests
	/// </summary>
	public Dictionary<string, string>? Headers { get; }

	/// <summary>
	/// Whether or not the URL matches the provider
	/// </summary>
	/// <param name="url">The URL of the manga</param>
	/// <returns>Whether or not it matches and the unique ID of the manga for the source</returns>
	(bool matches, string? part) MatchesProvider(string url);

	/// <summary>
	/// Fetches a manga definition from the given source
	/// </summary>
	/// <param name="id">The ID of the manga to fetch</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The manga or null if something went wrong</returns>
	Task<ImportManga?> Manga(string id, CancellationToken token);

	/// <summary>
	/// Fetches the pages for a specific chapter of a manga from the given source
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token);

	/// <summary>
	/// Gets a rate limiter for fetching images from the source
	/// </summary>
	/// <param name="url">The URL of the image being fetched</param>
	/// <returns>The rate limiter to use for fetching images</returns>
	RateLimiter GetRateLimiter(string url);
}

/// <summary>
/// An alternative manga source interface that uses URLs instead of IDs
/// </summary>
public interface IMangaUrlSource : IMangaSource
{
	/// <summary>
	/// Fetches the page for a specific chapter of a manga from the given source
	/// </summary>
	/// <param name="url">The URL of the page the chapters are on</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<ImportPage[]> ChapterPages(string url, CancellationToken token);
}

/// <summary>
/// Represents a manga source that can be scanned for new manga to add to the database
/// </summary>
public interface IIndexableMangaSource : IMangaSource
{
	/// <summary>
	/// Triggers the indexing process for the source
	/// </summary>
	/// <param name="source">The source being indexed</param>
	/// <param name="token">The token for when to stop processing</param>
	/// <returns>The updated manga</returns>
	IAsyncEnumerable<ImportManga> Index(LoaderSource source, CancellationToken token);
}

/// <summary>
/// Represents a manga source that has a default content rating
/// </summary>
public interface IRatedSource
{
	/// <summary>
	/// The rating to apply
	/// </summary>
	ContentRating DefaultRating { get; }
}

/// <summary>
/// Represents a manga source that requires the use of FlareSolverr to fetch images
/// </summary>
public interface IFlareImageSource
{
	/// <summary>
	/// Whether or not to use flare images for this source.
	/// </summary>
	bool UseFlareImages { get; }
}


