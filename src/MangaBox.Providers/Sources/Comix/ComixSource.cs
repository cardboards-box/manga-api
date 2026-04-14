using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources.Comix;

using Utilities.Flare;

public interface IComixSource : IMangaSource, IFlareImageSource
{

}

internal class ComixSource(
	ComixApiService _api,
	ILogger<ComixSource> _logger) : IComixSource
{
	private static RateLimiter? _limiter;

	public string HomeUrl => "https://comix.to";

	public string Provider => "comix-to";

	public string? Referer => HomeUrl;

	public string Name => "Comix.to";

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => PolyfillExtensions.HEADERS_FOR_REFERS;

	public bool UseFlareImages => true;

	public async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var chapter = await _api.Chapter(chapterId, token);
		if (chapter?.Result?.Images is null)
		{
			_logger.LogWarning("Chapter not found: {ChapterId}", chapterId);
			return [];
		}

		return [..chapter.Result.Images
			.Select(i => new ImportPage(i.Url, (int)i.Width, (int)i.Height))];
	}

	public async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		async IAsyncEnumerable<ImportChapter> GetChapters(Comix<Comix.Manga> manga, [EnumeratorCancellation] CancellationToken token)
		{
			await foreach (var chapter in _api.AllChapters(id, token))
			{
				yield return new ImportChapter
				{
					Title = chapter.Name,
					Url = $"{HomeUrl}/title/{manga.Result.HashId}-{manga.Result.Slug}/{chapter.ChapterId}-chapter-{chapter.Number}",
					Id = chapter.ChapterId.ToString(),
					Number = chapter.Number,
					Volume = chapter.Volume == 0 ? null : chapter.Volume,
					ExternalUrl = null,
					Attributes = [],
				};
			}
		}

		var manga = await _api.Manga(id, token);
		if (manga is null)
		{
			_logger.LogWarning("Manga not found: {MangaId}", id);
			return null;
		}

		return new ImportManga
		{
			Title = manga.Result.Title,
			Id = manga.Result.HashId,
			Provider = Provider,
			HomePage = $"{HomeUrl}/title/{manga.Result.HashId}-{manga.Result.Slug}",
			Cover = manga.Result.Poster.Large,
			Description = manga.Result.Synopsis,
			AltTitles = manga.Result.AltTitles,
			Tags = [],
			Chapters = await GetChapters(manga, token).OrderBy(t => t.Number).ToListA(),
			Nsfw = manga.Result.IsNsfw,
			Attributes = [],
			SourceCreated = DateTimeOffset.FromUnixTimeSeconds(manga.Result.CreatedAt).DateTime,
			OrdinalVolumeReset = false,
		};
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		string URL = $"{HomeUrl}/title/";
		if (!url.StartsWith(URL, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var parts = url[URL.Length..].Split("-", StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return (false, null);

		return (true, parts.First());
	}

	public RateLimiter GetRateLimiter(string _) => _limiter ??= PolyfillExtensions.DefaultRateLimiter();
}

internal class ComixApiService(
	IFlareSolverService _flare,
	ILogger<ComixApiService> _logger)
{
	public const string BASE_URL = "https://comix.to/api/v2";

	private readonly FlareSolverInstance _api = _flare.Limiter();

	public static string WrapUrl(string url)
	{
		if (url.StartsWith("http"))
			return url;
		return $"{BASE_URL}/{url.TrimStart('/')}";
	}

	public async Task<Comix<T>?> Get<T>(string url, CancellationToken token)
	{
		var data = string.Empty;
		try
		{
			url = WrapUrl(url);
			var response = await _api.GetHtml(url, token);
			if (response == null)
			{
				_logger.LogWarning("Received null response from COMIX API for URL: {Url}", url);
				return default;
			}

			data = response.DocumentNode?.InnerText("//pre") 
				?? response.FlareSolution.Response 
				?? string.Empty;
			var output = JsonSerializer.Deserialize<Comix<T>>(data);
			if (output is null) return default;

			output.Solver = response.FlareSolution;
			return output;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching COMIX URL: {Url} >> {Data}", url, data);
			return default;
		}
	}

	public Task<Comix<Comix.Manga>?> Manga(string id, CancellationToken token)
	{
		return Get<Comix.Manga>($"manga/{id}", token: token);
	}

	public Task<Comix<Comix.Chapter>?> Chapter(string id, CancellationToken token)
	{
		return Get<Comix.Chapter>($"chapters/{id}", token: token);
	}

	public Task<Comix<Comix.ChapterList>?> Chapters(string id, int page, CancellationToken token)
	{
		var url = ComixToSigner.SignChapter(id, page);
		return Get<Comix.ChapterList>(url, token: token);
	}

	public async IAsyncEnumerable<Comix.Chapter> AllChapters(string mangaId, [EnumeratorCancellation] CancellationToken token)
	{
		int page = 1;
		while (true)
		{
			token.ThrowIfCancellationRequested();
			var result = await Chapters(mangaId, page, token);
			if (result == null || result.Result.Items.Length == 0)
				yield break;

			foreach (var chap in result.Result.Items)
			{
				chap.Solver = result.Solver;
				yield return chap;
			}

			if (page >= result.Result.Pagination.LastPage)
				yield break;

			page++;
		}
	}
}


