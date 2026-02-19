using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Utilities.Flare;
using static Services.MangaSource;

public interface IChapmanganatoSource : IMangaUrlSource { }

public class ChapmanganatoSource(
	IFlareSolverService _flare,
	ILogger<ChapmanganatoSource> _logger) : IChapmanganatoSource
{
	private static RateLimiter? _limiter;

	public string HomeUrl => "https://www.natomanga.com";

	public string ChapterBaseUri => $"{HomeUrl}";

	public string MangaBaseUri => $"{HomeUrl}/manga";

	public string Provider => "chapmanganato";

	public string Name => "NatoManga (Used to be: ChapMangaNato)";

	public string? Referer => HomeUrl + "/";

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => new()
	{
		{"Sec-Fetch-Dest", "image"},
		{"Sec-Fetch-Mode", "no-cors"},
		{"Sec-Fetch-Site", "cross-site"},
		{"Sec-Ch-Ua-Platform", "\"Windows\""},
		{"Sec-Ch-Ua-Mobile", "?0"},
		{"Sec-Ch-Ua", "\"Not A(Brand\";v=\"99\", \"Opera GX\";v=\"107\", \"Chromium\";v=\"121\""},
	};

	private readonly FlareSolverInstance _api = _flare.Limiter();

	public async Task<MangaChapterPage[]> ChapterPages(string url, CancellationToken token)
	{
		var doc = await _api.GetHtml(url, token: token);
		if (doc == null) return [];

		return doc
				.DocumentNode
				.SelectNodes("//div[@class=\"container-chapter-reader\"]/img")
				.Select(t => new MangaChapterPage(t.GetAttributeValue("src", "")))
				.ToArray();
	}

	public Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{ChapterBaseUri}/manga/{mangaId}/{chapterId}";
		return ChapterPages(url, token);
	}

	public void FillDetails(Manga manga, HtmlDocument doc)
	{
		var nodes = doc.DocumentNode
			.SelectNodes("//ul[@class='manga-info-text']/li")?
			.ToArray() ?? [];
		foreach (var node in nodes)
		{
			var text = node.InnerText.HTMLDecode().Trim();
			if (!text.Contains(':')) continue;

			var parts = text.Split(':');
			var key = parts[0].Trim().ToLower();
			var value = string.Join(':', parts.Skip(1)).Trim();

			if (key.Contains("genres"))
				manga.Tags = value.Split(',').Select(t => t.Trim()).ToArray();
		}

		var description = doc.DocumentNode
			.SelectSingleNode("//div[@class='main-wrapper']/div[@class='leftCol']" +
				"/div[@id='contentBox']");
		manga.Description = description?.InnerText.HTMLDecode().Trim() ?? string.Empty;
	}

	public async Task<Manga?> Manga(string id, CancellationToken token)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}/{id}";
		var doc = await _api.GetHtml(url, token);
		if (doc == null) return null;

		var manga = new Manga
		{
			Title = doc.DocumentNode.SelectSingleNode("//ul[@class=\"manga-info-text\"]/li/h1")?.InnerText ?? "",
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = doc.DocumentNode.SelectSingleNode("//div[@class=\"manga-info-pic\"]/img")?.GetAttributeValue("src", "") ?? "",
		};

		FillDetails(manga, doc);

		var chapterEntries = doc.DocumentNode.SelectNodes("//div[@class='chapter-list']/div[@class='row']/span/a");
		if (chapterEntries is null)
		{
			_logger.LogWarning("No chapters found for manga {MangaId} at {Url}", id, url);
			return null;
		}

		int num = chapterEntries.Count;
		foreach (var chapter in chapterEntries)
		{
			var a = chapter;
			var href = a.GetAttributeValue("href", "").TrimStart('/');
			var c = new MangaChapter
			{
				Title = a.InnerText.Trim(),
				Url = href,
				Number = num--,
				Id = href.Split('/').Last(),
			};

			manga.Chapters.Add(c);
		}

		manga.Chapters = manga.Chapters.OrderBy(t => t.Number).ToList();

		return manga;
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.ToLower().StartsWith(MangaBaseUri, StringComparison.InvariantCultureIgnoreCase);
		if (!matches) return (false, null);

		var parts = url[MangaBaseUri.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		if (parts.Length == 1) return (true, parts.First());

		return (false, null);
	}

	public RateLimiter GetRateLimiter() => _limiter ??= PolyfillExtensions.DefaultRateLimiter();
}
