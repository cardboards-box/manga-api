using MManga = MangaDexSharp.Manga;

namespace CardboardBox.Manga.ReverseImageSearch.MatchApi;

using Database;
using Models;

public interface IMatchIndexingService
{
    Task<bool> IndexManga(string id);

    Task IndexLatest();

    Task<int> FixCoverArt();
}

public class MatchIndexingService : IMatchIndexingService
{
    private readonly IMatchService _match;
    private readonly IMangaDex _md;
    private readonly ILogger _logger;
    private readonly IDbService _db;

    public MatchIndexingService(
        IMatchService match, 
        IMangaDex md,
        ILogger<MatchIndexingService> logger,
        IDbService db)
    {
        _match = match;
        _md = md;
        _logger = logger;
        _db = db;
    }

    public async Task<bool> IndexManga(string id)
    {
        var latest = await _md.Chapter.List(new ChaptersFilter
        {
            Manga = id
        });

        if (latest == null || latest.Data.Count == 0)
        {
            _logger.LogWarning("Manga match indexing: No new chapters found.");
            return false;
        }
        try
        {
            await IndexChapterList(latest, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while indexing manga: {id}", id);
            return false;
        }

        return true;
    }

    public async Task IndexChapterList(ChapterList latest, bool reindex = false)
    {
        await PolyfillCoverArt(latest);

        var chapIds = latest.Data.Select(t => t.Id).ToArray();
        var existings = (await _db.Cache.DetermineExisting(chapIds)).ToDictionary(t => t.Chapter.SourceId);

        int pageRequests = 0;
        foreach (var chapter in latest.Data)
        {
            var existing = existings.ContainsKey(chapter.Id) ? existings[chapter.Id] : null;
            var manga = GetMangaRel(chapter);

            if (manga == null)
            {
                _logger.LogWarning("Manga match indexing: Couldn't find manga relationship to chapter: " + chapter.Id);
                continue;
            }

            if (existing != null && !reindex) continue;

            if (pageRequests >= 35)
            {
                _logger.LogDebug($"Manga match indexing: Delaying indexing due to rate-limits >> {manga.Attributes.Title.PreferedOrFirst(t => t.Key == "en").Value} ({manga.Id}) >> {chapter.Attributes.Title ?? chapter.Attributes.Chapter} ({chapter.Id})");
                await Task.Delay(60 * 1000);
                pageRequests = 0;
            }

            if (!string.IsNullOrEmpty(chapter.Attributes.ExternalUrl))
            {
                _logger.LogWarning($"Manga match indexing: External URL detected, skipping: {manga.Attributes.Title.PreferedOrFirst(t => t.Key == "en").Value} ({manga.Id}) >> {chapter.Attributes.Title} ({chapter.Id})");
                continue;
            }

            var pages = await _md.Pages.Pages(chapter.Id);
            pageRequests++;
            if (pages == null || pages.Images.Length == 0)
            {
                _logger.LogWarning("Manga match indexing: Couldn't find any pages for chapter: " + chapter.Id);
                continue;
            }

            var (dbChap, dbManga) = await Convert(chapter, manga, pages.Images);

            await _match.IndexPageProxy(dbManga.Cover, new MangaMetadata
            {
                Id = dbManga.Cover.MD5Hash(),
                Source = "mangadex",
                Url = dbManga.Cover,
                Type = MangaMetadataType.Cover,
                MangaId = manga.Id,
            }, dbManga.Referer);

            for (var i = 0; i < dbChap.Pages.Length; i++)
            {
                var url = dbChap.Pages[i];
                var meta = new MangaMetadata
                {
                    Id = url.MD5Hash(),
                    Source = "mangadex",
                    Url = url,
                    Type = MangaMetadataType.Page,
                    MangaId = manga.Id,
                    ChapterId = chapter.Id,
                    Page = i + 1,
                };

                await _match.IndexPageProxy(url, meta, dbManga.Referer);
            }

            _logger.LogDebug($"Manga match indexing: Indexed chapter >> {dbManga.Title} ({dbManga.SourceId}) >> {dbChap.Title} ({dbChap.SourceId})");
        }
    }

    public async Task IndexLatest()
    {
        var latest = await ChaptersLatest();
        if (latest == null || latest.Data.Count == 0)
        {
            _logger.LogWarning("Manga match indexing: No new chapters found.");
            return;
        }

        await IndexChapterList(latest);
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

        var manga = await _md.Manga.List(new MangaFilter { Ids = ids.Distinct().ToArray() });
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

    public static MManga? GetMangaRel(Chapter chapter)
    {
        var m = chapter.Relationships.FirstOrDefault(t => t is MManga);
        if (m == null) return null;

        return (MManga)m;
    }

    public async Task<(DbMangaChapter chapter, DbManga manga)> Convert(Chapter chapter, MManga manga, string[] pages)
    {
        var m = await Convert(manga);
        var c = await Convert(chapter, m.Id, pages);
        return (c, m);
    }

    public async Task<DbManga> Convert(MManga manga)
    {
        var item = MangaExtensions.ConvertCache(manga);
        item.Id = await _db.Cache.Upsert(item);
        return item;
    }

    public async Task<DbMangaChapter> Convert(Chapter chapter, long mangaId, string[] pages)
    {
        var item = MangaExtensions.ConvertCache(chapter);
        item.Pages = pages;
        item.MangaId = mangaId;
        item.Id = await _db.Cache.Upsert(item);
        return item;
    }

    public async Task<int> FixCoverArt()
    {
        var badCovers = await _db.Cache.BadCoverArt();
        if (badCovers.Length == 0)
        {
            _logger.LogInformation("No bad covers found!");
            return 0;
        }

        int count = 0;
        var chunkCounts = (int)Math.Ceiling((double)badCovers.Length / 100);
        var chunks = badCovers.Select(t => t.SourceId).Distinct().Split(chunkCounts).ToArray();
        foreach (var chunk in chunks)
        {
            var manga = await _md.Manga.List(new MangaFilter { Ids = chunk });

            if (manga == null || manga.Data.Count == 0)
            {
                _logger.LogInformation("No bad cover relationships / manga found!");
                continue;
            }

            foreach (var m in manga.Data)
            {
                await Convert(m);
                count++;
            }
        }

        _logger.LogInformation("Bad covers fixed (hopefully?) {count}", count);
        return count;
    }

    public Task<ChapterList> ChaptersLatest(ChaptersFilter? filter = null)
    {
        filter ??= new ChaptersFilter();
        filter.Limit = 100;
        filter.Order = new() { [ChaptersFilter.OrderKey.updatedAt] = OrderValue.desc };
        filter.Includes = new[] { MangaIncludes.manga };
        filter.TranslatedLanguage = new[] { "en" };
        filter.IncludeExternalUrl = false;
        return _md.Chapter.List(filter);
    }
}
