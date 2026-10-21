namespace MangaBox.Providers.Sources;

using Utilities.Flare;

/// <summary>Loads MangaKatana metadata and chapter image links.</summary>
public interface IMangaKatanaSource : IMangaSource { }

/// <summary>
/// Reads MangaKatana's embedded page arrays through HTTP before using a browser
/// for challenges. Every refresh requests fresh HTML; browser retries and media
/// downloads must not delay an otherwise readable chapter.
/// </summary>
/// <param name="_flare">The browser fallback for protected responses.</param>
/// <param name="_http">The shared HTTP request service.</param>
/// <param name="_logger">The source timing and fallback logger.</param>
public class MangaKatanaSource(IFlareSolverService _flare, IApiService _http,
	ILogger<MangaKatanaSource> _logger) : BaseMangaSource<MangaKatanaSource>, IMangaKatanaSource
{
	public override string HomeUrl => "https://mangakatana.com/";

	public string MangaBaseUri => "https://mangakatana.com/manga/";

	public override string Provider => "mangakatana";

	public override string Name => "MangaKatana";

	/// <summary>
	/// Fetches fresh HTML with a bounded direct request and one browser fallback.
	/// A successful HTTP status alone is insufficient: challenge pages can be 200.
	/// </summary>
	private async Task<HtmlDocument> GetHtml(string url, Func<HtmlDocument, bool> usable, CancellationToken token)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			using var response = await _http.Get(url, request => request.Timeout(15)
				.UserAgent(UserAgent ?? string.Empty), token);
			if (response?.IsSuccessStatusCode == true)
			{
				var document = new HtmlDocument();
				document.LoadHtml(await response.Content.ReadAsStringAsync(token));
				if (usable(document))
				{
					_logger.LogInformation("MangaKatana HTTP refresh: {Milliseconds:F0} ms for {Url}", watch.Elapsed.TotalMilliseconds, url);
					return document;
				}
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
		{
			_logger.LogWarning(ex, "MangaKatana direct HTTP failed for {Url}", url);
		}

		_logger.LogInformation("MangaKatana browser fallback for {Url}", url);
		var result = await _flare.Get(url, timeout: 20_000, disableMedia: true, token: token);
		if (result?.Solution is null || result.Solution.Status is < 200 or >= 300)
			throw new HttpRequestException($"MangaKatana browser request failed for {url}.");
		var fallback = new HtmlDocument();
		fallback.LoadHtml(result.Solution.Response);
		if (!usable(fallback)) throw new InvalidDataException($"MangaKatana returned no usable content for {url}.");
		_logger.LogInformation("MangaKatana browser refresh: {Milliseconds:F0} ms for {Url}", watch.Elapsed.TotalMilliseconds, url);
		return fallback;
	}

	/// <summary>Parses the embedded JavaScript page array while allowing whitespace around the assignment.</summary>
	internal static ImportPage[] ParsePages(HtmlDocument document)
	{
		var script = document.DocumentNode.SelectNodes("//script")?
			.Select(node => Regex.Match(node.InnerHtml, @"\bthzq\s*=\s*\[(.*?)\]", RegexOptions.Singleline))
			.FirstOrDefault(match => match.Success);
		if (script is null) return [];
		return Regex.Matches(script.Groups[1].Value, "['\"](?<url>https?://[^'\"]+)['\"]")
			.Select(match => new ImportPage(match.Groups["url"].Value.Replace("\\/", "/"))).ToArray();
	}

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = $"{MangaBaseUri}{mangaId}/{chapterId}";
		var doc = await GetHtml(url, document => ParsePages(document).Length > 0, token);
		return ParsePages(doc);
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.ToLower().StartsWith("http") ? id : $"{MangaBaseUri}{id}";
		var doc = await GetHtml(url, document => document.DocumentNode
			.SelectSingleNode("//table[contains(@class,'uk-table')]//div[@class='chapter']/a") is not null, token);

		var manga = new ImportManga
		{
			Title = doc.Attribute("//meta[@property='og:title']", "content") ?? string.Empty,
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = [doc.Attribute("//div[@class='d-cell-medium media']/div[@class='cover']/img", "src") ?? string.Empty],
			Description = doc.InnerHtml("//div[@class='summary']/p") ?? string.Empty
		};

		var meta = doc.DocumentNode.SelectNodes("//ul[@class='meta d-table']/li[@class='d-row-small']");
		foreach (var li in meta)
		{
			var clone = li.Copy();
			var label = clone.InnerText("//div[@class='d-cell-small label']")?.ToLower()?.Trim();

			switch (label)
			{
				case "alt name(s):":
					manga.AltTitles = clone
						.InnerText("//div[@class='alt_name']")?
						.Split(';')
						.Select(t => t.Trim())
						.ToArray() ?? Array.Empty<string>();
					continue;
				case "genres:":
					manga.Tags = clone.SelectNodes("//a[@class='text_0']").Select(t => t.InnerText.Trim()).ToArray();
					continue;
			}
		}

		var chapters = doc.DocumentNode.SelectNodes("//table[@class='uk-table uk-table-striped']/tbody/tr/td/div[@class='chapter']/a");
		var i = chapters.Count;
		foreach (var chap in chapters)
		{
			i--;
			var href = chap.GetAttributeValue("href", "");
			var name = chap.InnerText;

			manga.Chapters.Add(new ImportChapter
			{
				Title = name.Trim(),
				Url = href.Trim(),
				Id = href.Trim('/').Split('/').Last(),
				Number = i
			});
		}

		manga.Chapters = manga.Chapters.OrderBy(t => t.Number).ToList();
		return manga;
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		var matches = url.ToLower().StartsWith(HomeUrl.ToLower());
		if (!matches) return (false, null);

		var parts = url.Remove(0, HomeUrl.Length).Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return (false, null);

		var domain = parts.First();
		if (domain.ToLower() != "manga") return (false, null);

		if (parts.Length >= 2)
			return (true, parts[1]);

		return (false, null);
	}

}
