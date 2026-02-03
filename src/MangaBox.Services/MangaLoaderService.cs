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
	/// <returns>A boxed result of <see cref="MangaBoxType{MbManga}"/></returns>
	Task<Boxed> Load(Guid? profileId, string url, bool force);

	/// <summary>
	/// Attempts to refresh a manga from it's source
	/// </summary>
	/// <param name="profileId">The ID of the user making the request</param>
	/// <param name="mangaId">The ID of the manga to refresh</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbManga}"/></returns>
	Task<Boxed> Refresh(Guid? profileId, Guid mangaId);

	/// <summary>
	/// Gets the pages for the given chapter ID
	/// </summary>
	/// <param name="chapterId">The ID of the chapter to get pages for</param>
	/// <param name="force">Whether or not to force the refresh to happen</param>
	/// <returns>A boxed result of <see cref="MangaBoxType{MbChapter}"/></returns>
	Task<Boxed> Pages(Guid chapterId, bool force);
}

internal class MangaLoaderService(
	IDbService _db,
	IEnumerable<IMangaSource> _sources,
	IMangaPublishService _publish) : IMangaLoaderService
{
	public async Task<Boxed> Refresh(Guid? profileId, Guid mangaId)
	{
		var manga = await _db.Manga.Fetch(mangaId);
		if (manga is null) 
			return Boxed.NotFound(nameof(MbManga), "Manga was not found.");

		return await Load(profileId, manga.Url, true);
	}

	public async Task<Boxed> Load(Guid? profileId, string url, bool force)
	{
		var source = await FindSource(url);
		if (source is null)
			return Boxed.NotFound(nameof(MbSource), "Manga source was not found.");

		if (!force)
		{
			var existing = await _db.Manga.FetchWithRelationships(source.Id, source.Info.Id);
			if (existing is not null) return Boxed.Ok(existing);
		}

		return await Load(source, profileId);
	}

	public async Task<Boxed> Pages(Guid chapterId, bool force)
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

		var source = await FindSource(manga.Url);
		if (source is null)	
			return Boxed.NotFound(nameof(MbSource), "Manga source was not found.");

		MangaSource.MangaChapterPage[] pages;
		if (source.Service is IMangaUrlSource url)
		{
			if (string.IsNullOrEmpty(chapter.Url))
				return Boxed.NotFound(nameof(MbChapter), "Chapter URL is empty.");

			pages = await url.ChapterPages(chapter.Url);
		}
		else
			pages = await source.Service.ChapterPages(manga.OriginalSourceId, chapter.SourceId);

		if (pages.Length == 0)
			return Boxed.NotFound(nameof(MbChapter), "No pages were found for chapter.");

		for(var i = 0; i < pages.Length; i++)
		{
			var page = pages[i];
			await _db.Image.Upsert(new()
			{
				Url = page.Page,
				MangaId = manga.Id,
				ChapterId = chapter.Id,
				Ordinal = i + 1,
				ImageWidth = page.Width,
				ImageHeight = page.Height,
			});
		}

		result = await _db.Chapter.FetchWithRelationships(chapterId);
		if (result is null)
			return Boxed.NotFound(nameof(MbChapter), "Chapter was not found after updating pages.");
		return Boxed.Ok(result);
	}

	public async Task<IdSource?> FindSource(string url)
	{
		await foreach(var source in Sources())
		{
			var (matches, part) = source.Service.MatchesProvider(url);
			if (!matches || string.IsNullOrEmpty(part)) continue;

			return new(part, source);
		}

		return null;
	}

	public static void Clean(MangaSource.Manga manga)
	{
		static string? Decode(string? text)
		{
			if (string.IsNullOrEmpty(text)) return text;
			return HttpUtility.HtmlDecode(text).Trim('\n');
		}

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
		manga.Chapters = manga.Chapters
			.GroupBy(c => c.Id)
			.Select(g => g.First())
			.ToList();

		foreach (var chapter in manga.Chapters)
		{
			chapter.Title = Decode(chapter.Title?.Trim().ForceNull());
			chapter.Langauge = chapter.Langauge?.Trim().ForceNull() ?? "en";
		}
	}

	public async Task<Boxed> Load(IdSource found, Guid? profileId)
	{
		var before = await found.Service.Manga(found.Id);
		if (before is null)
			return Boxed.NotFound(nameof(MbManga), "Could not load manga from source");
		
		Clean(before);
		var json = JsonSerializer.Serialize(before);
		var result = await _db.Manga.UpsertJson(found.Info.Id, json);
		if (result is null)
			return Boxed.Exception("An unknown error occurred while upserting the manga.");

		await _db.MangaExt.Update(result.Manga.Id);

		var manga = await _db.Manga.FetchWithRelationships(result.Manga.Id);
		if (manga is null)
			return Boxed.Exception("An unknown error occurred while fetching the manga after upsert.");

		await (result.MangaIsNew ? _publish.MangaNew(manga) : _publish.MangaUpdate(manga));
		foreach(var chapter in result.ChaptersNew)
			await _publish.ChapterNew(chapter);
		return Boxed.Ok(manga);
	}

	public async IAsyncEnumerable<Source> Sources()
	{
		var sources = await _db.Source.Get();
		foreach(var source in _sources)
		{
			var match = sources.FirstOrDefault(s => s.Slug.EqualsIc(source.Provider));
			if (match is null)
			{
				match = new()
				{
					Slug = source.Provider,
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

			yield return new Source(match, source);
		}
	}

	internal record class Source(MbSource Info, IMangaSource Service);

	internal record class IdSource(string Id, Source Source)
	{
		public MbSource Info => Source.Info;
		public IMangaSource Service => Source.Service;
	}
}