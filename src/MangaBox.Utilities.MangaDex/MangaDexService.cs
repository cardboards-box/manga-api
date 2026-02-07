using MangaDexSharp;
using System.Threading.RateLimiting;

namespace MangaBox.Utilities.MangaDex;

/// <summary>
/// A service for interacting with MangaDex
/// </summary>
public interface IMangaDexService
{
	/// <summary>
	/// Fetches all of the manga with the given IDs
	/// </summary>
	/// <param name="ids">The IDs of the manga</param>
	/// <returns>All of the manga data</returns>
	Task<MangaList> AllManga(params string[] ids);

	/// <summary>
	/// Fetches a manga by it's ID
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The manga data</returns>
	Task<MangaDexRoot<Manga>> Manga(string id);

	/// <summary>
	/// Searches for manga by the given filter
	/// </summary>
	/// <param name="filter">The manga filter</param>
	/// <returns>All of the manga</returns>
	Task<MangaList> Search(MangaFilter filter);

	/// <summary>
	/// Searches for manga by it's title
	/// </summary>
	/// <param name="title">The title</param>
	/// <returns>All of the manga</returns>
	Task<MangaList> Search(string title);

	/// <summary>
	/// Searches for chapters by the given filter
	/// </summary>
	/// <param name="filter">The chapter filter</param>
	/// <returns>All of the chapters</returns>
	Task<ChapterList> Chapters(ChaptersFilter? filter = null);

	/// <summary>
	/// Searches for chapters of the given manga ID
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="filter">The chapter filter</param>
	/// <returns>All of the chapters</returns>
	Task<ChapterList> Chapters(string id, MangaFeedFilter? filter = null);

	/// <summary>
	/// Pages through the chapters of the given manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="limit">The number of chapters to return</param>
	/// <param name="offset">The number of chapters to skip</param>
	/// <returns>All of the chapters</returns>
	Task<ChapterList> Chapters(string id, int limit = 500, int offset = 0);

	/// <summary>
	/// Gets a chapter by it's ID
	/// </summary>
	/// <param name="id">The ID of the chapter</param>
	/// <param name="includes">Additional data to include</param>
	/// <returns>The chapter data</returns>
	Task<MangaDexRoot<Chapter>> Chapter(string id, string[]? includes = null);

	/// <summary>
	/// Pages through the chapters of the given manga
	/// </summary>
	/// <param name="filter">The chapter filter</param>
	/// <returns>All of the chapters</returns>
	Task<ChapterList> ChaptersLatest(ChaptersFilter? filter = null);

	/// <summary>
	/// Fetches the pages of the given chapter ID
	/// </summary>
	/// <param name="id">The ID of the chapter</param>
	/// <returns>The pages data</returns>
	Task<Pages> Pages(string id);
}

internal class MangaDexService(
	IMangaDex _md,
	ILogger<MangaDexService> _logger,
	[FromKeyedServices(MangaDexService.KEY)] RateLimiter _limiter) : IMangaDexService
{
	public const string KEY = "MangaDexApiRateLimiter";

	public async Task<IDisposable> Limit(CancellationToken token)
	{
		return await _limiter.AcquireAsync(1, token);
	}

	public async Task<T> Limit<T>(Func<IMangaDex, Task<T>> func, CancellationToken token)
	{
		using var limit = await _limiter.AcquireAsync(1, token);
		return await func(_md);
	}

	public Task<MangaList> Search(string title) => Search(new MangaFilter() { Title = title });

	public Task<MangaList> Search(MangaFilter filter) => Limit(md => md.Manga.List(filter), CancellationToken.None);

	public Task<MangaList> AllManga(params string[] ids) => Search(new MangaFilter { Ids = ids });

	public Task<MangaDexRoot<Manga>> Manga(string id)
	{
		var includes = new[] { MangaIncludes.cover_art, MangaIncludes.author, MangaIncludes.artist, MangaIncludes.scanlation_group, MangaIncludes.tag, MangaIncludes.chapter };
		return Limit(md => md.Manga.Get(id, includes), CancellationToken.None);
	}

	public Task<ChapterList> Chapters(ChaptersFilter? filter = null) => Limit(md => md.Chapter.List(filter), CancellationToken.None);

	public Task<ChapterList> Chapters(string id, MangaFeedFilter? filter = null) => Limit(md => md.Manga.Feed(id, filter), CancellationToken.None);

	public Task<ChapterList> Chapters(string id, int limit = 500, int offset = 0)
	{
		var filter = new MangaFeedFilter
		{
			Order = new()
			{
				[MangaFeedFilter.OrderKey.volume] = OrderValue.asc,
				[MangaFeedFilter.OrderKey.chapter] = OrderValue.asc,
			},
			Limit = limit,
			Offset = offset
		};

		return Chapters(id, filter);
	}

	public Task<ChapterList> ChaptersLatest(ChaptersFilter? filter = null)
	{
		filter ??= new ChaptersFilter();
		filter.Limit = 100;
		filter.Order = new() { [ChaptersFilter.OrderKey.updatedAt] = OrderValue.desc };
		filter.Includes = new[] { MangaIncludes.manga };
		filter.TranslatedLanguage = new[] { "en" };
		filter.IncludeExternalUrl = false;
		return Chapters(filter);
	}

	public Task<MangaDexRoot<Chapter>> Chapter(string id, string[]? includes = null)
	{
		return Limit(md => md.Chapter.Get(id, includes), CancellationToken.None);
	}

	public async Task<Pages> Pages(string id)
	{
		try
		{
			return await Limit(md => md.Pages.Pages(id), CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while getting pages for {Id}", id);
			return new Pages() { };
		}
	}

}
