using F23.StringSimilarity;

namespace CardboardBox.Manga.ReverseImageSearch;

using Database;
using GoogleVision;
using MatchApi;
using Models;
using SauceNao;

using static ImageSearchResults;

public interface IReverseSearchService
{
    Task<ImageSearchResults> Search(MemoryStream stream, string filename);
    Task<ImageSearchResults> Search(string image);
}

public class ReverseSearchService : IReverseSearchService
{
    private readonly IGoogleVisionService _vision;
    private readonly IMatchService _match;
    private readonly ISauceNaoApiService _sauce;
    private readonly ILogger _logger;
    private readonly IMangaDex _md;
    private readonly IDbService _db;

    public ReverseSearchService(
        IGoogleVisionService vision, 
        IMatchService match, 
        ISauceNaoApiService sauce, 
        ILogger<ReverseSearchService> logger, 
        IMangaDex md,
        IDbService db)
    {
        _vision = vision;
        _match = match;
        _sauce = sauce;
        _logger = logger;
        _md = md;
        _db = db;
    }

    public async Task<ImageSearchResults> Search(MemoryStream stream, string filename)
    {
        var results = new ImageSearchResults();

        using var second = new MemoryStream();
        await stream.CopyToAsync(second);

        stream.Position = 0;
        second.Position = 0;

        await HandleFallback(stream, filename, results);
        if (!AnyMatches(results))
            await HandleVision(second, filename, results);

        DetermineBestGuess(results);

        return results;
    }

    public async Task<ImageSearchResults> Search(string image)
    {
        if (Uri.IsWellFormedUriString(image, UriKind.Absolute))
        {
            var results = new ImageSearchResults();

            await HandleFallback(image, results);

            if (!AnyMatches(results))
                await HandleSauceNao(image, results);

            if (!AnyMatches(results))
                await HandleVision(image, results);

            DetermineBestGuess(results);

            return results;
        }

        var raw = await MdSearch(image);
        if (raw == null || raw.Data == null || raw.Data.Count == 0)
            return new ImageSearchResults();

        var data = raw.Data
            .Select(MangaExtensions.Convert)
            .ToArray();

        var rank = Rank(image, data)
            .OrderByDescending(t => t.Compute)
            .DistinctBy(t => t.Manga.Id)
            .Select(t => new BaseResult
            {
                Manga = t.Manga,
                ExactMatch = t.Compute > 1,
                Score = t.Compute,
                Source = "title lookup"
            }).ToList();

        return new ImageSearchResults()
        {
            Textual = rank,
            BestGuess = rank.FirstOrDefault()?.Manga
        };
    }

    public static void DetermineBestGuess(ImageSearchResults results)
    {
        if (results.Match.Count == 0 && results.Vision.Count == 0) return;

        var exact = results.Match.FirstOrDefault(t => t.ExactMatch)?.Manga;
        if (exact != null)
        {
            results.BestGuess = exact;
            return;
        }

        exact = results.Vision.FirstOrDefault(t => t.ExactMatch)?.Manga;
        if (exact != null)
        {
            results.BestGuess = exact;
            return;
        }

        var bestFall = results.Match.OrderByDescending(t => t.Score).FirstOrDefault();
        var bestVisi = results.Vision.OrderByDescending(t => t.Score).FirstOrDefault();

        if (bestFall == null && bestVisi != null)
        {
            results.BestGuess = bestVisi.Manga;
            return;
        }

        if (bestFall != null && bestVisi == null)
        {
            results.BestGuess = bestFall.Manga;
            return;
        }

        if (bestFall == null || bestVisi == null) return;

        results.BestGuess = bestVisi.Score > bestFall.Score ? bestVisi.Manga : bestFall.Manga;
    }

    public static bool AnyMatches(ImageSearchResults results)
    {
        var all = results.All.ToArray();
        if (all.Length == 0) return false;

        var exact = all.Any(t => t.ExactMatch);
        if (exact) return true;

        return all.Any(t => t.Score > 80);
    }

    #region Fallback
    public Task HandleFallback(MemoryStream stream, string filename, ImageSearchResults output)
    {
        return HandleFallback(_match.Search(stream, filename), output);
    }

    public Task HandleFallback(string image, ImageSearchResults output)
    {
        return HandleFallback(_match.Search(image), output);
    }

    public async Task HandleFallback(Task<MatchMeta<MangaMetadata>[]> task, ImageSearchResults output)
    {
        var results = await task;
        if (results.Length == 0) return;

        var ids = results
            .Select(t => t.Metadata?.MangaId ?? "")
            .Where(t => !string.IsNullOrEmpty(t))
            .ToArray();

        var manga = await _db.Cache.ByIds(ids);

        foreach (var res in results)
        {
            var m = manga.FirstOrDefault(t => t.SourceId == res.Metadata?.MangaId);
            var trimmed = m == null ? null : (TrimmedManga)m;

            var fallback = new FallbackResult
            {
                Score = res.Score,
                ExactMatch = res.Score >= 100,
                Manga = trimmed,
                Metadata = res.Metadata
            };

            output.Match.Add(fallback);
        }
    }
    #endregion

    #region Vision
    public Task HandleVision(string image, ImageSearchResults output)
    {
        return HandleVision(_vision.ExecuteVisionRequest(image), output);
    }

    public Task HandleVision(MemoryStream stream, string filename, ImageSearchResults output)
    {
        return HandleVision(_vision.ExecuteVisionRequest(stream, filename), output);
    }

    public async Task HandleVision(Task<VisionResults?> task, ImageSearchResults output)
    {
        var vision = await task;
        if (vision == null) return;

        for (var i = 0; i < vision.WebPages.Length && i < 3; i++)
        {
            var (url, title) = vision.WebPages[i];
            var filtered = PurgeVisionTitle(title);

            if (string.IsNullOrEmpty(filtered)) continue;

            var results = await SearchMangaDex(filtered, url, title).ToArrayAsync();

            output.Vision.AddRange(results);
            if (results.Any(t => t.ExactMatch)) break;
        }

        output.Vision = output.Vision
            .OrderByDescending(t => t.Score)
            .DistinctBy(t => t.Url)
            .ToList();
    }

    public async IAsyncEnumerable<VisionResult> SearchMangaDex(string title, string url, string originalTitle)
    {
        var search = Array.Empty<DbManga>();

        try
        {
            search = (await MdSearch(title))
                .Data
                .Select(MangaExtensions.Convert)
                .ToArray();
            if (search == null || search.Length == 0) yield break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while searching mangadex: {title}");
            yield break;
        }

        var sort = Rank(title, search)
            .OrderByDescending(t => t.Compute);

        int count = 0;
        foreach (var (compute, match, manga) in sort)
        {
            if (count >= 3) yield break;

            var output = new VisionResult
            {
                Url = url,
                Title = originalTitle,
                FilteredTitle = title,
                Score = compute * 100,
                ExactMatch = compute > 1,
                Manga = (TrimmedManga)manga
            };

            yield return output;
            count++;
        }
    }

    public static IEnumerable<(double Compute, bool Main, DbManga Manga)> Rank(string title, DbManga[] manga)
    {
        var check = new NormalizedLevenshtein();

        foreach (var m in manga)
        {
            var mt = PurgeVisionTitle(m.Title);
            if (mt == title)
            {
                yield return (1.2, true, m);
                continue;
            }

            yield return (check.Distance(title, mt), true, m);

            foreach (var t in m.AltTitles)
            {
                var mtt = PurgeVisionTitle(t);
                if (mtt == title)
                {
                    yield return (1.1, false, m);
                    continue;
                }

                yield return (check.Distance(title, mtt), false, m);
            }
        }
    }

    public static string PurgeVisionTitle(string title)
    {
        var regexPurgers = new[]
        {
            ("manga", "manga[a-z]{1,}\\b")
        };

        var purgers = new[]
        {
            ("chapter", new[] { "chapter" }),
            ("chap", new[] { "chap" }),
            ("read", new[] { "read" }),
            ("online", new[] { "online" }),
            ("manga", new[] { "manga" }),
            ("season", new[] { "season" }),
            ("facebook", new[] { "facebook" })
        };

        title = title.ToLower();

        if (title.Contains("<"))
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(title);
            title = doc.DocumentNode.InnerText;
        }

        if (title.Contains('&')) title = WebUtility.HtmlDecode(title);

        foreach (var (text, regex) in regexPurgers)
            if (title.Contains(text))
                title = Regex.Replace(title, regex, string.Empty);

        foreach (var (text, replacers) in purgers)
            if (title.Contains(text))
                foreach (var regex in replacers)
                    title = title.Replace(regex, "").Trim();

        title = new string(title
            .Select(t => !char.IsPunctuation(t) &&
                !char.IsNumber(t) &&
                !char.IsSymbol(t) ? t : ' ').ToArray());

        while (title.Contains("  "))
            title = title.Replace("  ", " ").Trim();

        return title;
    }
    #endregion

    #region SauceNao
    public async Task HandleSauceNao(string image, ImageSearchResults results)
    {
        var res = await _sauce.Get(image);
        if (res == null || res.Results.Length == 0) return;

        var mdRes = res
            .Results
            .Where(t => t.Data.ExternalUrls.Any(a => a.ToLower().Contains("mangadex")))
            .Select(t => (t, double.TryParse(t.Header.Similarity, out var sim) ? sim : -1))
            .Where(t => t.Item2 > 70)
            .OrderByDescending(t => t.Item2 != -1)
            .Select(t => t.t)
            .FirstOrDefault();

        if (mdRes == null || string.IsNullOrEmpty(mdRes.Data.Source)) return;

        var title = mdRes.Data.Source;
        var raw = await MdSearch(title);
        if (raw == null || raw.Data == null || raw.Data.Count == 0)
            return;

        var data = raw.Data.Select(MangaExtensions.Convert).ToArray();

        var rank = Rank(image, data)
            .OrderByDescending(t => t.Compute)
            .DistinctBy(t => t.Manga.Id)
            .Select(t => new BaseResult
            {
                Manga = t.Manga,
                ExactMatch = t.Compute > 1,
                Score = t.Compute,
                Source = "sauce nao"
            }).ToList();

        results.Textual = rank;
        results.BestGuess = rank.FirstOrDefault()?.Manga;
    }
    #endregion

    public Task<MangaList> MdSearch(string title)
    {
        return _md.Manga.List(new MangaFilter { Title = title });
    }
}
