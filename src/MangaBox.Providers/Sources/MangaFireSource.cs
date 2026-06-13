namespace MangaBox.Providers.Sources;

using Models.Types;

public interface IMangaFireSource : IMangaSource { }

internal class MangaFireSource(
	IApiService _api,
	ILogger<MangaFireSource> _logger) : BaseMangaSource<MangaFireSource>, IMangaFireSource
{
	public override string HomeUrl => "https://mangafire.to/";

	public string MangaBaseUri => $"{HomeUrl}manga/";

	public override string Provider => "mangafire";

	public override string Name => "MangaFire (mangafire.to)";

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var url = chapterId.StartsWithIc("http")
			? chapterId
			: $"{HomeUrl}read/{mangaId.Trim('/')}/{chapterId.Trim('/')}";

		var doc = await GetHtml(url, token);
		var pages = doc is null ? [] : ParsePages(doc);
		if (pages.Length > 0)
			return pages;

		pages = await GetAjaxPages(doc, url, mangaId, chapterId, token);
		if (pages.Length > 0)
			return pages;

		return [];
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWithIc("http")
			? id
			: $"{MangaBaseUri}{id.Trim('/')}";

		var doc = await GetHtml(url, token);
		if (doc is null)
			return null;

		var sourceId = MangaIdFromUrl(url) ?? id.Trim('/');
		var title = Clean(doc.InnerText("//h1[@itemprop='name']"))
			?? CleanTitle(doc.Attribute("//meta[@property='og:title']", "content"))
			?? string.Empty;

		if (string.IsNullOrWhiteSpace(title))
		{
			_logger.LogWarning("Could not find title for MangaFire id: {MangaId}", id);
			return null;
		}

		var details = ParseDetails(doc);
		var tags = details.GetValuesOrDefault("genre")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return new ImportManga
		{
			Id = sourceId,
			Title = title,
			Provider = Provider,
			HomePage = url,
			Cover = [BestCover(doc) ?? string.Empty],
			Description = Description(doc),
			AltTitles = SplitAltTitles(Clean(doc.InnerText("//h6"))),
			Authors = details.GetValuesOrDefault("author").ToArray(),
			Tags = tags,
			Rating = IsSuggestive(tags) ? ContentRating.Suggestive : ContentRating.Safe,
			Nsfw = IsSuggestive(tags),
			Referer = Referer,
			Attributes = BuildAttributes(doc, details),
			Chapters = ParseChapters(doc, sourceId)
		};
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var id = MangaIdFromUrl(url) ?? MangaIdFromReadUrl(url);
		return string.IsNullOrWhiteSpace(id)
			? (false, null)
			: (true, id);
	}

	private async Task<HtmlDocument?> GetHtml(string url, CancellationToken token)
	{
		try
		{
			return await _api.GetHtml(url, HeadersFor(url), token);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to retrieve MangaFire page: {Url}", url);
			return null;
		}
	}

	private async Task<ImportPage[]> GetAjaxPages(HtmlDocument? doc, string readerUrl, string mangaId, string chapterId, CancellationToken token)
	{
		var chapterDataId = doc is null ? null : ReaderChapterDataId(doc, readerUrl);
		chapterDataId ??= await FetchChapterDataId(mangaId, chapterId, token);
		if (string.IsNullOrWhiteSpace(chapterDataId))
			return [];

		var apiUrl = $"{HomeUrl}ajax/read/chapter/{chapterDataId}?vrf={GenerateVrf($"chapter@{chapterDataId}")}";
		using var client = new HttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
		request.Headers.Referrer = new Uri(readerUrl);
		request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
		request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

		try
		{
			using var response = await client.SendAsync(request, token);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("MangaFire page API returned {StatusCode} for {Url}", response.StatusCode, apiUrl);
				return [];
			}

			await using var stream = await response.Content.ReadAsStreamAsync(token);
			using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
			if (!json.RootElement.TryGetProperty("result", out var result) ||
				!result.TryGetProperty("images", out var images) ||
				images.ValueKind != JsonValueKind.Array)
				return [];

			var pages = new List<ImportPage>();
			foreach (var image in images.EnumerateArray())
			{
				if (image.ValueKind != JsonValueKind.Array ||
					image.GetArrayLength() == 0 ||
					image[0].ValueKind != JsonValueKind.String)
					continue;

				var url = CleanUrl(image[0].GetString());
				if (string.IsNullOrWhiteSpace(url))
					continue;

				var page = new ImportPage(AbsoluteUrl(url));
				page.Headers.Add(new("ordinal", (pages.Count + 1).ToString(CultureInfo.InvariantCulture)));
				if (image.GetArrayLength() > 2 && image[2].TryGetInt32(out var offset) && offset > 0)
					page.Headers.Add(new("scramble-offset", offset.ToString(CultureInfo.InvariantCulture)));

				pages.Add(page);
			}

			return [..pages];
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to retrieve MangaFire page API: {Url}", apiUrl);
			return [];
		}
	}

	private async Task<string?> FetchChapterDataId(string mangaId, string chapterId, CancellationToken token)
	{
		var shortId = MangaShortId(mangaId);
		var parts = chapterId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (string.IsNullOrWhiteSpace(shortId) || parts.Length < 2)
			return null;

		var lang = parts[0];
		var chapterPart = parts[1];
		var apiUrl = $"{HomeUrl}ajax/read/{shortId}/chapter/{lang}?vrf={GenerateVrf($"{shortId}@chapter@{lang}")}";

		using var client = new HttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
		request.Headers.Referrer = new Uri($"{MangaBaseUri}{mangaId.Trim('/')}");
		request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
		request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

		try
		{
			using var response = await client.SendAsync(request, token);
			if (!response.IsSuccessStatusCode)
				return null;

			await using var stream = await response.Content.ReadAsStreamAsync(token);
			using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
			if (!json.RootElement.TryGetProperty("result", out var result) ||
				!result.TryGetProperty("html", out var htmlElement) ||
				htmlElement.ValueKind != JsonValueKind.String)
				return null;

			var html = htmlElement.GetString();
			if (string.IsNullOrWhiteSpace(html))
				return null;

			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var targetPath = $"/read/{mangaId.Trim('/')}/{lang}/{chapterPart}";
			var anchor = doc.DocumentNode
				.SelectNodes("//a[@data-id]")
				?.FirstOrDefault(x => x.GetAttributeValue("href", string.Empty)
					.TrimEnd('/')
					.Equals(targetPath, StringComparison.OrdinalIgnoreCase));

			return Clean(anchor?.GetAttributeValue("data-id", null!));
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to retrieve MangaFire chapter id API: {Url}", apiUrl);
			return null;
		}
	}

	private Action<HttpRequestMessage> HeadersFor(string referer) => request =>
	{
		request.Headers.Referrer = new Uri(referer);
	};

	private static List<ImportChapter> ParseChapters(HtmlDocument doc, string sourceId)
	{
		var anchors = doc.DocumentNode
			.SelectNodes($"//div[contains(@class,'tab-content') and @data-name='chapter']//a[contains(@href,'/read/{sourceId}/') and contains(@href,'/chapter-')]")
			?.ToArray() ?? [];

		if (anchors.Length == 0)
		{
			anchors = doc.DocumentNode
				.SelectNodes($"//a[contains(@href,'/read/{sourceId}/') and contains(@href,'/chapter-')]")
				?.ToArray() ?? [];
		}

		var output = new List<ImportChapter>();
		foreach (var anchor in anchors)
		{
			var href = AbsoluteUrl(anchor.GetAttributeValue("href", string.Empty));
			var parts = UrlParts(href);
			var readIndex = Array.FindIndex(parts, x => x.Equals("read", StringComparison.OrdinalIgnoreCase));
			if (readIndex < 0 || parts.Length <= readIndex + 3)
				continue;

			var lang = parts[readIndex + 2].ToLowerInvariant();
			if (!lang.Equals("en", StringComparison.OrdinalIgnoreCase))
				continue;

			var chapterPart = parts[readIndex + 3];
			var title = Clean(anchor.SelectSingleNode(".//span[1]")?.InnerText)
				?? Clean(anchor.GetAttributeValue("title", string.Empty))
				?? chapterPart;
			var number = ExtractChapterNumber(anchor.Ancestors("li").FirstOrDefault()?.GetAttributeValue("data-number", null!))
				?? ExtractChapterNumber(chapterPart)
				?? ExtractChapterNumber(title);

			output.Add(new ImportChapter
			{
				Title = title,
				Url = href,
				Id = $"{lang}/{chapterPart}",
				Number = number ?? output.Count + 1,
				Language = lang,
				Attributes = ParseChapterAttributes(anchor)
			});
		}

		return [..output
			.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
			.Select(x => x.First())
			.OrderBy(x => x.Number)];
	}

	private static List<ImportAttribute> ParseChapterAttributes(HtmlNode anchor)
	{
		var date = Clean(anchor.SelectSingleNode(".//span[2]")?.InnerText);
		return string.IsNullOrWhiteSpace(date)
			? []
			: [new ImportAttribute("Date", date)];
	}

	private static ImportPage[] ParsePages(HtmlDocument doc)
	{
		var images = doc.DocumentNode
			.SelectNodes("//div[@id='page-wrapper']//img | //ul[@id='page-items']//img")
			?.ToArray() ?? [];

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return [..images
			.Select(GetImageUrl)
			.Select(AbsoluteUrl)
			.Where(x => !string.IsNullOrWhiteSpace(x) && !IsSiteAsset(x) && seen.Add(x))
			.Select((x, i) =>
			{
				var page = new ImportPage(x);
				page.Headers.Add(new("ordinal", (i + 1).ToString(CultureInfo.InvariantCulture)));
				return page;
			})];
	}

	private static string? GetImageUrl(HtmlNode image)
	{
		return First(
			image.GetAttributeValue("data-src", null!),
			image.GetAttributeValue("data-url", null!),
			image.GetAttributeValue("src", null!));
	}

	private static string? ReaderChapterDataId(HtmlDocument doc, string readerUrl)
	{
		var active = doc.DocumentNode.SelectSingleNode("//div[@id='number-panel']//a[contains(concat(' ', normalize-space(@class), ' '), ' active ')]");
		var dataId = Clean(active?.GetAttributeValue("data-id", null!));
		if (!string.IsNullOrWhiteSpace(dataId))
			return dataId;

		if (!Uri.TryCreate(readerUrl, UriKind.Absolute, out var uri))
			return null;

		var path = uri.AbsolutePath.TrimEnd('/');
		var matching = doc.DocumentNode
			.SelectNodes("//div[@id='number-panel']//a[@data-id]")
			?.FirstOrDefault(x =>
			{
				var href = x.GetAttributeValue("href", string.Empty);
				if (string.IsNullOrWhiteSpace(href))
					return false;

				href = AbsoluteUrl(href);
				return Uri.TryCreate(href, UriKind.Absolute, out var hrefUri) &&
					hrefUri.AbsolutePath.TrimEnd('/').Equals(path, StringComparison.OrdinalIgnoreCase);
			});

		return Clean(matching?.GetAttributeValue("data-id", null!));
	}

	private static string GenerateVrf(string input)
	{
		var bytes = Encoding.UTF8.GetBytes(Uri.EscapeDataString(input));
		var stages = new[]
		{
			new VrfStage("FgxyJUQDPUGSzwbAq/ToWn4/e8jYzvabE+dLMb1XU1o=", "yH6MXnMEcDVWO/9a6P9W92BAh1eRLVFxFlWTHUqQ474=", "l9PavRg=", ScheduleC),
			new VrfStage("CQx3CLwswJAnM1VxOqX+y+f3eUns03ulxv8Z+0gUyik=", "RK7y4dZ0azs9Uqz+bbFB46Bx2K9EHg74ndxknY9uknA=", "Ml2v7ag1Jg==", ScheduleY),
			new VrfStage("fAS+otFLkKsKAJzu3yU+rGOlbbFVq+u+LaS6+s1eCJs=", "rqr9HeTQOg8TlFiIGZpJaxcvAaKHwMwrkqojJCpcvoc=", "i/Va0UxrbMo=", ScheduleB),
			new VrfStage("Oy45fQVK9kq9019+VysXVlz1F9S1YwYKgXyzGlZrijo=", "/4GPpmZXYpn5RpkP7FC/dt8SXz7W30nUZTe8wb+3xmU=", "WFjKAHGEkQM=", ScheduleJ),
			new VrfStage("aoDIdXezm2l3HrcnQdkPJTDT8+W6mcl2/02ewBHfPzg=", "wsSGSBXKWA9q1oDJpjtJddVxH+evCfL5SO9HZnUDFU8=", "5Rr27rWd", ScheduleE),
		};

		foreach (var stage in stages)
		{
			bytes = Rc4(Convert.FromBase64String(stage.Key), bytes);
			bytes = Transform(bytes, Convert.FromBase64String(stage.Seed), Convert.FromBase64String(stage.Prefix), stage.Schedule);
		}

		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	private static byte[] Rc4(byte[] key, byte[] input)
	{
		var state = Enumerable.Range(0, 256).ToArray();
		var j = 0;
		for (var i = 0; i < 256; i++)
		{
			j = (j + state[i] + key[i % key.Length]) & 255;
			(state[i], state[j]) = (state[j], state[i]);
		}

		var output = new byte[input.Length];
		var x = 0;
		j = 0;
		for (var y = 0; y < input.Length; y++)
		{
			x = (x + 1) & 255;
			j = (j + state[x]) & 255;
			(state[x], state[j]) = (state[j], state[x]);
			var keyByte = state[(state[x] + state[j]) & 255];
			output[y] = (byte)((input[y] ^ keyByte) & 255);
		}

		return output;
	}

	private static byte[] Transform(byte[] input, byte[] seed, byte[] prefix, Func<int, int>[] schedule)
	{
		var output = new List<byte>(input.Length + prefix.Length);
		for (var i = 0; i < input.Length; i++)
		{
			if (i < prefix.Length)
				output.Add(prefix[i]);

			var value = (input[i] ^ seed[i % seed.Length]) & 255;
			output.Add((byte)(schedule[i % schedule.Length](value) & 255));
		}

		return [..output];
	}

	private static Func<int, int> Add8(int n) => c => (c + n) & 255;

	private static Func<int, int> Sub8(int n) => c => (c - n + 256) & 255;

	private static Func<int, int> Rotl8(int n) => c => ((c << n) | (c >> (8 - n))) & 255;

	private static Func<int, int> Rotr8(int n) => c => ((c >> n) | (c << (8 - n))) & 255;

	private static Func<int, int>[] ScheduleC =>
	[
		Sub8(223), Rotr8(4), Rotr8(4), Add8(234), Rotr8(7),
		Rotr8(2), Rotr8(7), Sub8(223), Rotr8(7), Rotr8(6)
	];

	private static Func<int, int>[] ScheduleY =>
	[
		Add8(19), Rotr8(7), Add8(19), Rotr8(6), Add8(19),
		Rotr8(1), Add8(19), Rotr8(6), Rotr8(7), Rotr8(4)
	];

	private static Func<int, int>[] ScheduleB =>
	[
		Sub8(223), Rotr8(1), Add8(19), Sub8(223), Rotl8(2),
		Sub8(223), Add8(19), Rotl8(1), Rotl8(2), Rotl8(1)
	];

	private static Func<int, int>[] ScheduleJ =>
	[
		Add8(19), Rotl8(1), Rotl8(1), Rotr8(1), Add8(234),
		Rotl8(1), Sub8(223), Rotl8(6), Rotl8(4), Rotl8(1)
	];

	private static Func<int, int>[] ScheduleE =>
	[
		Rotr8(1), Rotl8(1), Rotl8(6), Rotr8(1), Rotl8(2),
		Rotr8(4), Rotl8(1), Rotl8(1), Sub8(223), Rotl8(2)
	];

	private sealed record VrfStage(string Key, string Seed, string Prefix, Func<int, int>[] Schedule);

	private static Dictionary<string, List<string>> ParseDetails(HtmlDocument doc)
	{
		var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		var items = doc.DocumentNode.SelectNodes("//div[contains(@class,'meta')]/div") ?? Enumerable.Empty<HtmlNode>();

		foreach (var item in items)
		{
			var key = Clean(item.SelectSingleNode("./span[1]")?.InnerText)?.Trim(':').ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(key))
				continue;

			key = DetailKey(key) ?? key;
			var content = item.SelectSingleNode("./span[2]");
			var values = content?
				.SelectNodes(".//a")
				?.Select(x => Clean(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.ToList() ?? [];

			if (values.Count == 0)
			{
				var text = Clean(content?.InnerText);
				if (!string.IsNullOrWhiteSpace(text))
					values.Add(text);
			}

			if (values.Count > 0)
				output[key] = values;
		}

		return output;
	}

	private static string? DetailKey(string heading)
	{
		if (heading.Contains("author")) return "author";
		if (heading.Contains("published")) return "published";
		if (heading.Contains("genre")) return "genre";
		if (heading.Contains("mangazine")) return "magazine";
		return null;
	}

	private static List<ImportAttribute> BuildAttributes(HtmlDocument doc, Dictionary<string, List<string>> details)
	{
		var output = details
			.Where(x => x.Key is "published" or "magazine")
			.Select(x => new ImportAttribute(x.Key, string.Join(", ", x.Value)))
			.Where(x => !string.IsNullOrWhiteSpace(x.Value))
			.ToList();

		var status = Clean(doc.InnerText("//div[contains(@class,'info')]/p[1]"));
		if (!string.IsNullOrWhiteSpace(status))
			output.Add(new ImportAttribute("status", status));

		var type = Clean(doc.InnerText("//div[contains(@class,'min-info')]/a[1]"));
		if (!string.IsNullOrWhiteSpace(type))
			output.Add(new ImportAttribute("type", type));

		var rating = Clean(doc.InnerText("//span[contains(@class,'live-score')]"));
		if (!string.IsNullOrWhiteSpace(rating))
			output.Add(new ImportAttribute("rating", rating));

		return output;
	}

	private static string? Description(HtmlDocument doc)
	{
		return Clean(doc.InnerText("//div[@id='synopsis']//div[contains(@class,'modal-content')]"))
			?? Clean(doc.InnerText("//div[contains(@class,'description')]"))
			?? Clean(doc.Attribute("//meta[@property='og:description']", "content"));
	}

	private static string? BestCover(HtmlDocument doc)
	{
		var img = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'poster')]//img")
			?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'detail-bg')]//img");

		return AbsoluteUrl(First(
			img?.GetAttributeValue("data-src", null!),
			img?.GetAttributeValue("src", null!),
			doc.Attribute("//meta[@property='og:image']", "content")));
	}

	private static string[] SplitAltTitles(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? []
			: [..value
				.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Distinct(StringComparer.OrdinalIgnoreCase)];
	}

	private static double? ExtractChapterNumber(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var match = Regex.Match(value, @"\b(?:chapter|ch)\s*-?\s*(?<num>\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase);
		if (!match.Success)
			match = Regex.Match(value, @"\b(?<num>\d+(?:\.\d+)?)\b");

		return match.Success &&
			double.TryParse(match.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
				? number
				: null;
	}

	private static string? MangaIdFromUrl(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsMangaFireHost(uri.Host))
			return null;

		var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 || !parts[0].Equals("manga", StringComparison.OrdinalIgnoreCase))
			return null;

		return parts[1];
	}

	private static string? MangaIdFromReadUrl(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsMangaFireHost(uri.Host))
			return null;

		var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 || !parts[0].Equals("read", StringComparison.OrdinalIgnoreCase))
			return null;

		return parts[1];
	}

	private static string MangaShortId(string mangaId)
	{
		mangaId = mangaId.Trim('/');
		var index = mangaId.LastIndexOf('.');
		return index >= 0 && index < mangaId.Length - 1
			? mangaId[(index + 1)..]
			: mangaId;
	}

	private static bool IsMangaFireHost(string host)
	{
		return host.Equals("mangafire.to", StringComparison.OrdinalIgnoreCase) ||
			host.Equals("www.mangafire.to", StringComparison.OrdinalIgnoreCase);
	}

	private static string? CleanTitle(string? value)
	{
		value = Clean(value);
		return value?
			.Replace("Manga - Read Manga Online Free", "", StringComparison.InvariantCultureIgnoreCase)
			.Replace("| Read Online on MangaFire", "", StringComparison.InvariantCultureIgnoreCase)
			.Trim('-', '|', ':', ' ')
			.Trim();
	}

	private static bool IsSuggestive(IEnumerable<string> tags)
	{
		return tags.Any(x =>
			x.Equals("Ecchi", StringComparison.OrdinalIgnoreCase) ||
			x.Equals("Smut", StringComparison.OrdinalIgnoreCase) ||
			x.Equals("Adult", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsSiteAsset(string url)
	{
		return url.Contains("/assets/", StringComparison.OrdinalIgnoreCase) ||
			url.Contains("favicon", StringComparison.OrdinalIgnoreCase) ||
			url.Contains("logo", StringComparison.OrdinalIgnoreCase);
	}

	private static string[] UrlParts(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
			return [];

		return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
	}

	private static string AbsoluteUrl(string? value)
	{
		value = CleanUrl(value);
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		if (value.StartsWith("//", StringComparison.Ordinal))
			return $"https:{value}";

		if (Uri.TryCreate(value, UriKind.Absolute, out _))
			return value;

		return new Uri(new Uri("https://mangafire.to/"), value.TrimStart('/')).ToString();
	}

	private static string? Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = HtmlEntity.DeEntitize(value);
		value = Regex.Replace(value, @"\s+", " ");
		return value.Trim();
	}

	private static string CleanUrl(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		value = HtmlEntity.DeEntitize(value);
		value = value.Replace("\\/", "/", StringComparison.Ordinal);
		value = Regex.Replace(value, @"\s+", string.Empty);
		return value.Trim();
	}

	private static string? First(params string?[] values)
	{
		return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
	}
}
