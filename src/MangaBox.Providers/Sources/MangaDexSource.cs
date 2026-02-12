using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;
using Utilities.MangaDex;
using static Services.MangaSource;

//I need to fix the naming convention in MD#... It's way too generic :'(
using ChapterList = MangaDexSharp.ChapterList;
using CoverArtRelationship = MangaDexSharp.CoverArtRelationship;
using MangaFeedFilter = MangaDexSharp.MangaFeedFilter;
using MManga = MangaDexSharp.Manga;
using Chapter = MangaDexSharp.Chapter;
using RelatedDataRelationship = MangaDexSharp.RelatedDataRelationship;

public interface IMangaDexSource : IIndexableMangaSource
{

}

public class MangaDexSource(
	IMangaDexService _mangadex) : IMangaDexSource
{
	private const string DEFAULT_LANG = "en";
	public string HomeUrl => "https://mangadex.org";
	public string Provider => "mangadex";
	public string Name => "MangaDex";
	public string? Referer => null;
	public Dictionary<string, string>? Headers => null;
	public string? UserAgent => "manga-box";

	public async Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var pages = await _mangadex.Pages(chapterId);
		if (pages == null) return [];

		return [..pages.Images.Select(t => new MangaChapterPage(t))];
	}

	public async Task<Manga> Convert(MManga manga, CancellationToken token, List<MangaChapter>? chapters = null)
	{
		var id = manga.Id;
		var coverFile = (manga.Relationships.FirstOrDefault(t => t is CoverArtRelationship) as CoverArtRelationship)?.Attributes?.FileName;
		var coverUrl = $"{HomeUrl}/covers/{id}/{coverFile}";

		chapters ??= await GetChapters(id, token, DEFAULT_LANG)
			.OrderBy(t => t.Number)
			.ToListAsync(token);

		var title = DetermineTitle(manga);

		List<string> authors = [],
			artists = [];

		foreach(var rel in manga.Relationships)
		{
			if (rel is not MangaDexSharp.PersonRelationship person ||
				string.IsNullOrEmpty(person.Attributes?.Name))
				continue;

			if (person.Type == "author")
				authors.Add(person.Attributes.Name);
			else if (person.Type == "artist")
				artists.Add(person.Attributes.Name);
		}

		return new Manga
		{
			Title = title,
			Id = id,
			Provider = Provider,
			HomePage = $"{HomeUrl}/title/{id}",
			Cover = coverUrl,
			Authors = [..authors.Distinct()],
			Artists = [..artists.Distinct()],
			Description = manga.Attributes?.Description?.PreferredOrFirst(t => t.Key == DEFAULT_LANG).Value ?? string.Empty,
			AltTitles = manga.Attributes?.AltTitles.SelectMany(t => t.Values).Distinct().ToArray() ?? [],
			AltDescriptions = manga.Attributes?.Description?.Select(t => t.Value).ToArray() ?? [],
			Tags = manga
				.Attributes?
				.Tags
				.Select(t =>
					t.Attributes?
					 .Name
					 .PreferredOrFirst(t => t.Key == DEFAULT_LANG)
					 .Value ?? string.Empty).ToArray() ?? [],
			Chapters = chapters,
			Rating = Enum.TryParse<ContentRating>(manga.Attributes?.ContentRating?.ToString() ?? "safe", true, out var rating)
				? rating
				: ContentRating.Safe,
			Attributes = [..GetMangaAttributes(manga)],
			SourceCreated = manga.Attributes?.CreatedAt,
			OrdinalVolumeReset = manga.Attributes?.ChapterNumbersResetOnNewVolume ?? false,
		};
	}

	public async Task<Manga?> Manga(string id, CancellationToken token)
	{
		var manga = await _mangadex.Manga(id);
		if (manga == null || manga.Data == null || manga.Data.Attributes is null) return null;

		return await Convert(manga.Data, token);
	}

	public static string DetermineTitle(MManga manga)
	{
		manga.Attributes ??= new();
		var title = manga.Attributes.Title.PreferredOrFirst(t => t.Key.Equals(DEFAULT_LANG, StringComparison.CurrentCultureIgnoreCase));
		if (title.Key.Equals(DEFAULT_LANG, StringComparison.CurrentCultureIgnoreCase)) return title.Value;

		var prefered = manga.Attributes.AltTitles.FirstOrDefault(t => t.Keys.Contains(DEFAULT_LANG, StringComparer.InvariantCultureIgnoreCase));
		if (prefered != null)
			return prefered.PreferredOrFirst(t => t.Key.Equals(DEFAULT_LANG, StringComparison.CurrentCultureIgnoreCase)).Value;

		return title.Value;
	}

	public IEnumerable<MangaChapter> ConvertChapters(IEnumerable<Chapter> chapters)
	{
		var sortedChapters = chapters
				.Where(t => t.Attributes is not null)!
				.GroupBy(t => t.Attributes!.Chapter + t.Attributes.Volume)
				.Select(t => t.PreferredOrFirst(t => t.Attributes!.TranslatedLanguage == DEFAULT_LANG))
				.Where(t => t != null)
				.Select(t => new MangaChapter
				{
					Title = t?.Attributes!.Title ?? string.Empty,
					Url = $"{HomeUrl}/chapter/{t?.Id}",
					Id = t?.Id ?? string.Empty,
					Number = double.TryParse(t?.Attributes!.Chapter, out var a) ? a : 0,
					Volume = double.TryParse(t?.Attributes!.Volume, out var b) ? b : null,
					ExternalUrl = t?.Attributes!.ExternalUrl,
					Attributes = [.. GetChapterAttributes(t)]
				})
				.OrderBy(t => t.Volume)
				.OrderBy(t => t.Number);

		foreach (var chap in sortedChapters)
			yield return chap;
	}

	public async IAsyncEnumerable<MangaChapter> GetChapters(string id, [EnumeratorCancellation] CancellationToken token, params string[] languages)
	{
		var filter = new MangaFeedFilter { TranslatedLanguage = languages };
		while (true)
		{
			token.ThrowIfCancellationRequested();
			var chapters = await _mangadex.Chapters(id, filter);
			if (chapters == null || 
				chapters.Data is null || 
				chapters.Data.Count == 0) 
				yield break;

			foreach(var chapter in ConvertChapters(chapters.Data))
				yield return chapter;

			int current = chapters.Offset + chapters.Limit;
			if (chapters.Total <= current) yield break;

			filter.Offset = current;
		}
	}

	public static IEnumerable<MangaAttribute> GetChapterAttributes(Chapter? chapter)
	{
		if (chapter is null) yield break;

		if (!string.IsNullOrEmpty(chapter.Attributes?.TranslatedLanguage))
			yield return new MangaAttribute("Translated Language", chapter.Attributes.TranslatedLanguage);

		if (!string.IsNullOrEmpty(chapter.Attributes?.Uploader))
			yield return new MangaAttribute("Uploader", chapter.Attributes.Uploader);

		foreach (var relationship in chapter.Relationships)
		{
			switch (relationship)
			{
				case MangaDexSharp.PersonRelationship per:
					if (!string.IsNullOrEmpty(per.Attributes?.Name))
						yield return new MangaAttribute(per.Type == "author" ? "Author" : "Artist", per.Attributes.Name);
					break;
				case MangaDexSharp.ScanlationGroup grp:
					if (!string.IsNullOrEmpty(grp.Attributes?.Name))
						yield return new MangaAttribute("Scanlation Group", grp.Attributes.Name);
					if (!string.IsNullOrEmpty(grp.Attributes?.Website))
						yield return new MangaAttribute("Scanlation Link", grp.Attributes.Website);
					if (!string.IsNullOrEmpty(grp.Attributes?.Twitter))
						yield return new MangaAttribute("Scanlation Twitter", grp.Attributes.Twitter);
					if (!string.IsNullOrEmpty(grp.Attributes?.Discord))
						yield return new MangaAttribute("Scanlation Discord", grp.Attributes.Discord);
					break;
			}
		}
	}

	public static IEnumerable<MangaAttribute> GetMangaAttributes(MManga? manga)
	{
		if (manga == null) yield break;

		if (manga.Attributes?.ContentRating != null)
			yield return new("Content Rating", manga.Attributes.ContentRating?.ToString() ?? "");

		if (!string.IsNullOrEmpty(manga.Attributes?.OriginalLanguage))
			yield return new("Original Language", manga.Attributes.OriginalLanguage);

		if (manga.Attributes?.Status != null)
			yield return new("Status", manga.Attributes.Status?.ToString() ?? "");

		if (!string.IsNullOrEmpty(manga.Attributes?.State))
			yield return new("Publication State", manga.Attributes.State);

		foreach (var rel in manga.Relationships)
		{
			switch (rel)
			{
				case MangaDexSharp.ScanlationGroup group:
					if (!string.IsNullOrEmpty(group.Attributes?.Name))
						yield return new("Scanlation Group", group.Attributes.Name);
					break;
			}
		}
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		string URL = $"{HomeUrl}/title/";
		if (!url.StartsWith(URL, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var parts = url[URL.Length..].Split("/", StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return (false, null);

		return (true, parts.First());
	}

	public RateLimiter GetRateLimiter() => PolyfillExtensions.DefaultRateLimiter();

	public static MManga? GetMangaRel(Chapter chapter)
	{
		var m = chapter.Relationships.FirstOrDefault(t => t is MManga);
		if (m == null) return null;

		return (MManga)m;
	}

	public async Task PolyfillCoverArt(ChapterList data)
	{
		var ids = new List<string>();
		foreach (var chapter in data.Data)
		{
			var m = GetMangaRel(chapter);
			if (m == null) continue;

			ids.Add(m.Id);
		}

		var manga = await _mangadex.AllManga([..ids.Distinct()]);
		if (manga == null || manga.Data.Count == 0)
			return;

		foreach (var chapter in data.Data)
		{
			foreach (var rel in chapter.Relationships)
			{
				if (rel is not RelatedDataRelationship mr) continue;

				var existing = manga.Data.FirstOrDefault(t => t.Id == mr.Id);
				if (existing == null) continue;

				mr.Attributes = existing.Attributes;
				mr.Relationships = existing.Relationships;
			}
		}
	}

	public async IAsyncEnumerable<Manga> Index(LoaderSource source, [EnumeratorCancellation] CancellationToken token)
	{
		var latest = await _mangadex.ChaptersLatest();
		if (latest is null || latest.Data.Count == 0)
			yield break;

		await PolyfillCoverArt(latest);

		var grouped = latest.Data.GroupBy(t => GetMangaRel(t)?.Id)
			.Where(t => t.Key is not null);

		foreach(var chapters in grouped)
		{
			token.ThrowIfCancellationRequested();

			var chapter = chapters.First();
			var manga = GetMangaRel(chapter);
			if (manga == null) continue;

			yield return await Convert(manga, token, 
				[.. ConvertChapters(chapters)]);
		}
	}
}
