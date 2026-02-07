using System.Web;

namespace MangaBox.Services;

using Database;
using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// A service for loading manga from various sources
/// </summary>
public interface IMangaLoaderService
{
	/// <summary>
	/// Attempts to load a manga from the source
	/// </summary>
	/// <param name="profileId">The ID of the user making the request</param>
	/// <param name="url">The URL of the manga's home page</param>
	/// <param name="force">Whether or not to force the refresh to happen</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbManga}"/></returns>
	Task<Boxed> Load(Guid? profileId, string url, bool force, CancellationToken token);

	/// <summary>
	/// Attempts to load a manga from the given data
	/// </summary>
	/// <param name="input">The input data</param>
	/// <param name="sourceId">The ID of the source the manga is from</param>
	/// <param name="profileId">The ID of the user making the request</param>
	/// <param name="ids">Legacy IDs associated with the manga</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbManga}"/></returns>
	Task<Boxed> Load(MangaSource.Manga input, Guid sourceId, Guid? profileId, LegacyIds? ids);

	/// <summary>
	/// Attempts to refresh a manga from it's source
	/// </summary>
	/// <param name="profileId">The ID of the user making the request</param>
	/// <param name="mangaId">The ID of the manga to refresh</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbManga}"/></returns>
	Task<Boxed> Refresh(Guid? profileId, Guid mangaId, CancellationToken token);

	/// <summary>
	/// Gets the pages for the given chapter ID
	/// </summary>
	/// <param name="chapterId">The ID of the chapter to get pages for</param>
	/// <param name="force">Whether or not to force the refresh to happen</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbChapter}"/></returns>
	Task<Boxed> Pages(Guid chapterId, bool force, CancellationToken token);

	/// <summary>
	/// Runs the <see cref="IIndexableMangaSource.Index(LoaderSource, CancellationToken)"/> function for all applicable sources
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	Task RunIndex(CancellationToken token);
}

internal class MangaLoaderService(
	IDbService _db,
	ISourceService _sources,
	IMangaPublishService _publish) : IMangaLoaderService
{
	public async Task<Boxed> Refresh(Guid? profileId, Guid mangaId, CancellationToken token)
	{
		var manga = await _db.Manga.Fetch(mangaId);
		if (manga is null) 
			return Boxed.NotFound(nameof(MbManga), "Manga was not found.");

		return await Load(profileId, manga.Url, true, token);
	}

	public async Task<Boxed> Load(Guid? profileId, string url, bool force, CancellationToken token)
	{
		var source = await _sources.FindByUrl(url, token);
		if (source is null)
			return Boxed.NotFound(nameof(MbSource), "Manga source was not found.");

		if (!force)
		{
			var existing = await _db.Manga.FetchWithRelationships(source.Id, source.Info.Id);
			if (existing is not null) return Boxed.Ok(existing);
		}

		return await Load(source, profileId, token);
	}

	public async Task<Boxed> Pages(Guid chapterId, bool force, CancellationToken token)
	{
		var result = await _db.Chapter.FetchWithRelationships(chapterId);
		var chapter = result?.Entity;
		if (result is null || chapter is null)
			return Boxed.NotFound(nameof(MbChapter), "Chapter was not found.");

		if (!force && result.Any<MbImage>())
			return Boxed.Ok(result);

		var manga = result.GetItem<MbManga>();
		if (manga is null)
			return Boxed.NotFound(nameof(MbManga), "Manga was not found for chapter.");

		var source = await _sources.FindByUrl(manga.Url, token);
		if (source is null)	
			return Boxed.NotFound(nameof(MbSource), "Manga source was not found.");

		MangaSource.MangaChapterPage[] pages;
		if (source.Service is IMangaUrlSource url)
		{
			if (string.IsNullOrEmpty(chapter.Url))
				return Boxed.NotFound(nameof(MbChapter), "Chapter URL is empty.");

			pages = await url.ChapterPages(chapter.Url, token);
		}
		else
			pages = await source.Service.ChapterPages(manga.OriginalSourceId, chapter.SourceId, token);

		if (pages.Length == 0)
			return Boxed.NotFound(nameof(MbChapter), "No pages were found for chapter.");

		for(var i = 0; i < pages.Length; i++)
		{
			token.ThrowIfCancellationRequested();
			var page = pages[i];
			var id = await _db.Image.Upsert(new()
			{
				Url = page.Page,
				MangaId = manga.Id,
				ChapterId = chapter.Id,
				Ordinal = i + 1,
				ImageWidth = page.Width,
				ImageHeight = page.Height,
			});
			await _publish.NewImages.Publish(new(id, DateTime.UtcNow));
		}

		if (chapter.PageCount != pages.Length)
		{
			chapter.PageCount = pages.Length;
			await _db.Chapter.Update(chapter);
		}

		result = await _db.Chapter.FetchWithRelationships(chapterId);
		if (result is null)
			return Boxed.NotFound(nameof(MbChapter), "Chapter was not found after updating pages.");
		return Boxed.Ok(result);
	}

	public static void Clean(MangaSource.Manga manga, LegacyIds? ids)
	{
		static string? Decode(string? text)
		{
			if (string.IsNullOrEmpty(text)) return text;
			return HttpUtility.HtmlDecode(text).Trim('\n');
		}

		int? defaultId = null;

		manga.Title = Decode(manga.Title.Trim())!;
		manga.Description = Decode(manga.Description?.Trim().ForceNull());
		manga.AltDescriptions = manga.AltDescriptions
			.Select(d => Decode(d.Trim()))
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct()
			.ToArray()!;
		manga.AltTitles = manga.AltTitles
			.Select(t => Decode(t.Trim()))
			.Where(t => !string.IsNullOrEmpty(t))
			.Distinct()
			.ToArray()!;
		manga.Artists = manga.Artists
			.Select(d => Decode(d.Trim()))
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct()
			.ToArray()!;
		manga.Authors = manga.Authors
			.Select(d => Decode(d.Trim()))
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct()
			.ToArray()!;
		manga.Tags = manga.Tags
			.Select(d => Decode(d.Trim()))
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct()
			.ToArray()!;
		manga.Uploaders = manga.Uploaders
			.Select(d => Decode(d.Trim()))
			.Where(d => !string.IsNullOrEmpty(d))
			.Distinct()
			.ToArray()!;
		manga.Chapters = [.. manga.Chapters
			.GroupBy(c => c.Id)
			.Select(g => g.First())];
		manga.LegacyId ??= ids?.ParentId ?? defaultId;

		foreach (var chapter in manga.Chapters)
		{
			chapter.LegacyId ??= (ids?.ChildIds ?? []).TryGetValue(chapter.Id, out var childId) ? childId : defaultId;
			chapter.Title = Decode(chapter.Title?.Trim().ForceNull());
			chapter.Langauge = chapter.Langauge?.Trim().ForceNull() ?? "en";
		}

		var cr = manga.Attributes.FirstOrDefault(t => t.Name.EqualsIc("Content Rating"))?.Value;
		if (Enum.TryParse<ContentRating>(cr, true, out var rating))
			manga.Rating = rating;
	}

	public async Task<Boxed> Load(MangaSource.Manga input, Guid sourceId, Guid? profileId, LegacyIds? ids)
	{
		Clean(input, ids);
		var json = JsonSerializer.Serialize(input);
		var result = await _db.Manga.UpsertJson(sourceId, profileId, json);
		if (result is null)
			return Boxed.Exception("An unknown error occurred while upserting the manga.");

		await _db.MangaExt.Update(result.Manga.Id);

		var manga = await _db.Manga.FetchWithRelationships(result.Manga.Id);
		if (manga is null)
			return Boxed.Exception("An unknown error occurred while fetching the manga after upsert.");

		if (result.MangaIsNew)
			await _publish.NewManga.Publish(manga);
		foreach (var chapter in result.ChaptersNew)
			await _publish.NewChapters.Publish(chapter);
		return Boxed.Ok(manga);
	}

	public async Task<Boxed> Load(IdedSource found, Guid? profileId, CancellationToken token)
	{
		var before = await found.Service.Manga(found.Id, token);
		if (before is null)
			return Boxed.NotFound(nameof(MbManga), "Could not load manga from source");
		
		return await Load(before, found.Info.Id, profileId, null);
	}

	public Task RunIndex(CancellationToken token)
	{
		var opts = new ParallelOptions
		{
			MaxDegreeOfParallelism = 4,
			CancellationToken = token
		};
		return Parallel.ForEachAsync(_sources.All(token), opts, async (source, ct) =>
		{
			if (source.Service is not IIndexableMangaSource indexable)
				return;

			await foreach(var manga in indexable.Index(source, ct))
				await Load(manga, source.Info.Id, null, null);
		});
	}
}