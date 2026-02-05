using System.Threading.RateLimiting;

namespace MangaBox.Services;

/// <summary>
/// A service for interacting with sources
/// </summary>
public interface ISourceService
{
	/// <summary>
	/// Finds the source to load the given manga URL
	/// </summary>
	/// <param name="url">The URL of the manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>A source with a specific manga's ID</returns>
	Task<IdedSource?> FindByUrl(string url, CancellationToken token);

	/// <summary>
	/// Finds the source by it's slug
	/// </summary>
	/// <param name="slug">The slug of the source</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The source</returns>
	Task<LoaderSource?> FindBySlug(string slug, CancellationToken token);

	/// <summary>
	/// Finds the source by it's ID
	/// </summary>
	/// <param name="id">The ID of the source</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The source</returns>
	Task<LoaderSource?> FindById(Guid id, CancellationToken token);

	/// <summary>
	/// Gets all of the sources for loading manga
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>All of the sources</returns>
	IAsyncEnumerable<LoaderSource> All(CancellationToken token);

}

/// <inheritdoc cref="ISourceService" />
internal class SourceService(
	IDbService _db,
	IEnumerable<IMangaSource> _sources) : ISourceService
{
	private readonly ConcurrentDictionary<string, RateLimiter> _limiter = [];

	/// <inheritdoc />
	public async Task<LoaderSource?> FindById(Guid id, CancellationToken token)
	{
		await foreach (var source in All(token))
			if (source.Info.Id == id)
				return source;

		return null;
	}

	/// <inheritdoc />
	public async Task<LoaderSource?> FindBySlug(string slug, CancellationToken token)
	{
		await foreach(var source in All(token))
			if (source.Info.Slug.EqualsIc(slug))
				return source;

		return null;
	}

	/// <inheritdoc />
	public async Task<IdedSource?> FindByUrl(string url, CancellationToken token)
	{
		await foreach (var source in All(token))
		{
			token.ThrowIfCancellationRequested();
			var (matches, part) = source.Service.MatchesProvider(url);
			if (!matches || string.IsNullOrEmpty(part)) continue;

			return new(part, source);
		}

		return null;
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<LoaderSource> All([EnumeratorCancellation] CancellationToken token)
	{
		var sources = await _db.Source.Get();
		foreach (var source in _sources)
		{
			token.ThrowIfCancellationRequested();
			var match = sources.FirstOrDefault(s => s.Slug.EqualsIc(source.Provider));
			if (match is null)
			{
				match = new()
				{
					Slug = source.Provider,
					Name = source.Name,
					BaseUrl = source.HomeUrl,
					Enabled = true,
					IsHidden = false,
					Referer = source.Referer,
					UserAgent = source.UserAgent,
					Headers = source.Headers?.Select(h => new MbHeader
					{
						Key = h.Key,
						Value = h.Value,
					}).ToArray() ?? [],
				};
				match.Id = await _db.Source.Upsert(match);
			}

			var limiter = _limiter.GetOrAdd(match.Slug, _ => source.GetRateLimiter());
			yield return new(match, source, limiter);
		}
	}
}

/// <summary>
/// The source and it's service
/// </summary>
/// <param name="Info">The source information</param>
/// <param name="Service">The manga service</param>
/// <param name="RateLimits">The rate limits for image fetching</param>
public record class LoaderSource(MbSource Info, IMangaSource Service, RateLimiter RateLimits);

/// <summary>
/// A source with a specific manga's ID
/// </summary>
/// <param name="Id">The Id of the manga</param>
/// <param name="Source">The source and its service</param>
public record class IdedSource(string Id, LoaderSource Source)
{
	/// <summary>
	/// The source information
	/// </summary>
	public MbSource Info => Source.Info;

	/// <summary>
	/// The manga service
	/// </summary>
	public IMangaSource Service => Source.Service;

	/// <summary>
	/// The rate limits for image fetching
	/// </summary>
	public RateLimiter RateLimits => Source.RateLimits;
}
