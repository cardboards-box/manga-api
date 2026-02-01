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
			var existing = await _db.Manga.FetchWithRelationships(source.Value.id, source.Value.src.Info.Id);
			if (existing is not null) return Boxed.Ok(existing);
		}

		return await Load(source.Value.src, source.Value.id, profileId);
	}

	public async Task<(string id, Source src)?> FindSource(string url)
	{
		await foreach(var source in Sources())
		{
			var (matches, part) = source.Service.MatchesProvider(url);
			if (!matches || string.IsNullOrEmpty(part)) continue;

			return (part, source);
		}

		return null;
	}

	public async Task<Boxed> Load(Source src, string id, Guid? profileId)
	{
		var before = await src.Service.Manga(id);
		if (before is null)
			return Boxed.NotFound(nameof(MbManga), "Could not load manga from source");

		var now = DateTime.UtcNow.AddMinutes(-1);

		var after = await Convert(before, src.Info, profileId);
		await LoadChapters(before, after);
		var manga = await _db.Manga.FetchWithRelationships(after);
		if (manga is null)
			return Boxed.Exception("An unknown error occurred while fetching the manga after conversion.");

		if (manga.Entity.CreatedAt > now)
			await _publish.MangaNew(manga);
		else
			await _publish.MangaUpdate(manga);

		var newChapters = await _db.Chapter.Get(manga.Entity.Id, now);
		foreach(var chapter in newChapters)
			await _publish.ChapterNew(chapter);

		return Boxed.Ok(manga);
	}

	public async Task LoadChapters(MangaSource.Manga manga, Guid mangaId, string lang = "en")
	{
		foreach(var chapter in manga.Chapters)
		{
			var chap = new MbChapter
			{
				MangaId = mangaId,
				Title = chapter.Title,
				Url = chapter.Url,
				SourceId = chapter.Id,
				Ordinal = chapter.Number,
				Volume = chapter.Volume,
				Language = lang,
				ExternalUrl = chapter.ExternalUrl,
				Attributes = [..chapter.Attributes.Select(a => new MbAttribute()
				{
					Name = a.Name,
					Value = a.Value,
				})],
			};
			await _db.Chapter.Upsert(chap);
		}
	}

	public async Task<Guid> Convert(MangaSource.Manga manga, MbSource source, Guid? profileId)
	{
		var mid = await _db.Manga.Upsert(new()
		{
			Title = manga.Title,
			AltTitles = manga.AltTitles,
			Description = manga.Description,
			AltDescriptions = manga.AltDescriptions,
			Url = manga.HomePage,
			Attributes = [..manga.Attributes.Select(a => new MbAttribute()
			{
				Name = a.Name,
				Value = a.Value,
			})],
			ContentRating = manga.Rating,
			Nsfw = manga.Nsfw,
			SourceId = source.Id,
			OriginalSourceId = manga.Id,
			IsHidden = false,
			Referer = manga.Referer,
			SourceCreated = manga.SourceCreated,
			OrdinalVolumeReset = manga.OrdinalVolumeReset,
		});

		await _db.Image.Upsert(new()
		{
			Url = manga.Cover,
			MangaId = mid,
			Ordinal = 1
		});

		await AddRelationship(mid, manga.Authors, null, RelationshipType.Author);
		await AddRelationship(mid, manga.Artists, null, RelationshipType.Artist);
		await AddRelationship(mid, manga.Uploaders, null, RelationshipType.Uploader);

		if (!profileId.HasValue) return mid;

		var profile = await _db.Profile.Fetch(profileId.Value);
		if (profile is  null) return mid;

		await AddRelationship(mid, [profile.Username], profile.Id, RelationshipType.Uploader);
		return mid;
	}

	public Task AddRelationship(Guid mangaId, string[] names, Guid? pid, RelationshipType type)
	{
		return names.Select(async t =>
		{
			var id = await _db.Person.Upsert(new()
			{
				Name = t,
				Artist = type == RelationshipType.Artist,
				Author = type == RelationshipType.Author,
				ProfileId = pid,
				User = pid.HasValue
			});
			await _db.MangaRelationship.Upsert(new()
			{
				MangaId = mangaId,
				PersonId = id,
				Type = type,
			});
		}).WhenAll();
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
				};
				match.Id = await _db.Source.Insert(match);
			}

			yield return new Source(match, source);
		}
	}

	internal record class Source(MbSource Info, IMangaSource Service);
}