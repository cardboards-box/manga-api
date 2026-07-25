namespace MangaBox.Providers.Sources;

using Models.Types;

public interface IHentaiNexusSource : IMangaSource { }

public class HentaiNexusSource(
	IApiService _api,
	ILogger<HentaiNexusSource> _logger) : BaseMangaSource<HentaiNexusSource>, IHentaiNexusSource
{
	private const string DEFAULT_CHAPTER_TITLE = "Chapter 1";

	public override string HomeUrl => "https://hentainexus.com/";

	public string MangaBaseUri => $"{HomeUrl}view/";

	public override string Provider => "hentai-nexus";

	public override string Name => "HentaiNexus";

	public override ContentRating? DefaultRating => ContentRating.Pornographic;

	public override async Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		var id = IdFromValue(mangaId) ?? IdFromValue(chapterId);
		if (string.IsNullOrWhiteSpace(id))
			return [];

		var doc = await GetHtml($"{HomeUrl}read/{id}", token);
		return doc is null ? [] : ParsePages(doc);
	}

	public override async Task<ImportManga?> Manga(string id, CancellationToken token)
	{
		id = IdFromValue(id) ?? id.Trim('/');
		if (!IsNumericId(id))
			return null;

		var url = $"{MangaBaseUri}{id}";
		var doc = await GetHtml(url, token);
		if (doc is null)
			return null;

		var title = Clean(doc.InnerText("//h1[contains(concat(' ', normalize-space(@class), ' '), ' title ')]"))
			?? CleanTitle(doc.Attribute("//meta[@property='og:title']", "content"));
		var pages = ParsePages(doc);
		if (string.IsNullOrWhiteSpace(title) || pages.Length == 0)
		{
			_logger.LogWarning(
				"Skipping incomplete HentaiNexus gallery {GalleryId}: title={HasTitle}, pages={PageCount}",
				id,
				!string.IsNullOrWhiteSpace(title),
				pages.Length);
			return null;
		}

		var details = ParseDetails(doc);
		var artists = DetailValues(details, "artist")
			.Concat(DetailValues(details, "circle"))
			.Concat(DetailValues(details, "group"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var tags = DetailValues(details, "tags")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var cover = NormalizeUrl(First(
			doc.Attribute("//meta[@property='og:image']", "content"),
			doc.Attribute($"//a[@href='/read/{id}']//img", "src"),
			pages.FirstOrDefault()?.Page));

		return new ImportManga
		{
			Title = title,
			Id = id,
			Provider = Provider,
			HomePage = url,
			Cover = [cover ?? string.Empty],
			Description = DetailText(details, "description"),
			Artists = artists,
			Tags = tags,
			Rating = ContentRating.Pornographic,
			Nsfw = true,
			Referer = Referer,
			SourceCreated = ParseDate(DetailText(details, "published")),
			Attributes = BuildAttributes(details),
			Chapters =
			[
				new ImportChapter
				{
					Id = id,
					Title = DEFAULT_CHAPTER_TITLE,
					Number = 1,
					Volume = 1,
					Url = $"{HomeUrl}read/{id}",
					Language = "en",
					Pages = [..pages]
				}
			]
		};
	}

	public override (bool matches, string? part) MatchesProvider(string url)
	{
		var id = IdFromValue(url);
		return string.IsNullOrWhiteSpace(id)
			? (false, null)
			: (true, id);
	}

	private async Task<HtmlDocument?> GetHtml(string url, CancellationToken token)
	{
		try
		{
			return await _api.GetHtml(url, request =>
			{
				request.Headers.Referrer = new Uri(HomeUrl);
			}, token);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to retrieve HentaiNexus page: {Url}", url);
			return null;
		}
	}

	private static ImportPage[] ParsePages(HtmlDocument doc)
	{
		var payload = ReaderPayload(doc);
		if (string.IsNullOrWhiteSpace(payload))
			return [];

		var json = DecodeReaderPayload(payload);
		if (string.IsNullOrWhiteSpace(json))
			return [];

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var output = new List<ImportPage>();

		try
		{
			using var data = JsonDocument.Parse(json);
			if (data.RootElement.ValueKind != JsonValueKind.Array)
				return [];

			foreach (var item in data.RootElement.EnumerateArray())
			{
				var type = JsonString(item, "type");
				var urls = type?.Equals("spread", StringComparison.OrdinalIgnoreCase) == true
					? new[]
					{
						First(
							JsonString(item, "left_source"),
							JsonString(item, "left_fallback"),
							JsonString(item, "left_avif")),
						First(
							JsonString(item, "right_source"),
							JsonString(item, "right_fallback"),
							JsonString(item, "right_avif"))
					}
					: new[]
					{
						First(
							JsonString(item, "image_source"),
							JsonString(item, "image_fallback"),
							JsonString(item, "image_avif"))
					};

				foreach (var url in urls)
				{
					var normalized = NormalizeUrl(url);
					if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
						continue;

					var ordinal = output.Count + 1;
					var page = new ImportPage(normalized);
					page.Headers.Add(new("ordinal", ordinal.ToString(CultureInfo.InvariantCulture)));
					output.Add(page);
				}
			}
		}
		catch (JsonException)
		{
			return [];
		}

		return [..output];
	}

	private static string? ReaderPayload(HtmlDocument doc)
	{
		var scripts = doc.DocumentNode.SelectNodes("//script") ?? Enumerable.Empty<HtmlNode>();
		foreach (var script in scripts)
		{
			var match = Regex.Match(
				script.InnerText,
				@"initReader\(\s*""(?<payload>[A-Za-z0-9+/=]+)""",
				RegexOptions.CultureInvariant);
			if (match.Success)
				return match.Groups["payload"].Value;
		}

		return null;
	}

	private static string? DecodeReaderPayload(string payload)
	{
		byte[] data;
		try
		{
			data = Convert.FromBase64String(payload);
		}
		catch (FormatException)
		{
			return null;
		}

		const int KEY_LENGTH = 64;
		if (data.Length <= KEY_LENGTH)
			return null;

		var host = Encoding.ASCII.GetBytes("hentainexus.com");
		for (var index = 0; index < Math.Min(host.Length, KEY_LENGTH); index++)
			data[index] ^= host[index];

		int[] primes = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53];
		var checksum = 0;
		for (var index = 0; index < KEY_LENGTH; index++)
		{
			checksum ^= data[index];
			for (var bit = 0; bit < 8; bit++)
				checksum = (checksum & 1) != 0
					? checksum >>> 1 ^ 0x0c
					: checksum >>> 1;
		}

		var step = primes[checksum & 7];
		var state = Enumerable.Range(0, 256).ToArray();
		var stateIndex = 0;
		for (var index = 0; index < state.Length; index++)
		{
			stateIndex = (stateIndex + state[index] + data[index % KEY_LENGTH]) % state.Length;
			(state[index], state[stateIndex]) = (state[stateIndex], state[index]);
		}

		var output = new byte[data.Length - KEY_LENGTH];
		var firstIndex = 0;
		stateIndex = 0;
		var accumulator = 0;
		var key = 0;
		for (var index = 0; index < output.Length; index++)
		{
			firstIndex = (firstIndex + step) % state.Length;
			stateIndex = (accumulator + state[(stateIndex + state[firstIndex]) % state.Length]) % state.Length;
			accumulator = (accumulator + firstIndex + state[firstIndex]) % state.Length;
			(state[firstIndex], state[stateIndex]) = (state[stateIndex], state[firstIndex]);
			key = state[
				(stateIndex + state[
					(firstIndex + state[
						(key + accumulator) % state.Length
					]) % state.Length
				]) % state.Length
			];
			output[index] = (byte)(data[index + KEY_LENGTH] ^ key);
		}

		return Encoding.UTF8.GetString(output);
	}

	private static string? JsonString(JsonElement element, string property)
	{
		if (!element.TryGetProperty(property, out var value))
			return null;

		return value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
	}

	private static Dictionary<string, List<string>> ParseDetails(HtmlDocument doc)
	{
		var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		var rows = doc.DocumentNode
			.SelectNodes("//table[contains(concat(' ', normalize-space(@class), ' '), ' view-page-details ')]/tr")
			?? Enumerable.Empty<HtmlNode>();

		foreach (var row in rows)
		{
			var cells = row.SelectNodes("./td")?.ToArray() ?? [];
			if (cells.Length < 2)
				continue;

			var key = Clean(cells[0].InnerText)?.ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(key))
				continue;

			var values = cells[1]
				.SelectNodes(".//a")
				?.Select(x => CleanDisplayValue(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Cast<string>()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList() ?? [];

			if (values.Count == 0)
			{
				var value = Clean(cells[1].InnerText);
				if (!string.IsNullOrWhiteSpace(value))
					values.Add(value);
			}

			if (values.Count > 0)
				output[key] = values;
		}

		return output;
	}

	private static List<ImportAttribute> BuildAttributes(Dictionary<string, List<string>> details)
	{
		return [..details
			.Where(x => x.Key is not ("artist" or "circle" or "group" or "tags" or "description"))
			.SelectMany(x => x.Value.Select(value => new ImportAttribute(x.Key, value)))
			.Where(x => !string.IsNullOrWhiteSpace(x.Value))];
	}

	private static string? DetailText(Dictionary<string, List<string>> details, string key)
	{
		return details.TryGetValue(key, out var items)
			? items.FirstOrDefault()
			: null;
	}

	private static IEnumerable<string> DetailValues(Dictionary<string, List<string>> details, string key)
	{
		return details.TryGetValue(key, out var items)
			? items
			: [];
	}

	private static string? IdFromValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = value.Trim();
		if (IsNumericId(value.Trim('/')))
			return value.Trim('/');

		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
			(!uri.Host.Equals("hentainexus.com", StringComparison.OrdinalIgnoreCase) &&
			 !uri.Host.Equals("www.hentainexus.com", StringComparison.OrdinalIgnoreCase)))
			return null;

		var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 ||
			(!parts[0].Equals("view", StringComparison.OrdinalIgnoreCase) &&
			 !parts[0].Equals("read", StringComparison.OrdinalIgnoreCase)))
			return null;

		return IsNumericId(parts[1]) ? parts[1] : null;
	}

	private static bool IsNumericId(string value)
	{
		return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
	}

	private static DateTime? ParseDate(string? value)
	{
		return DateTime.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AllowWhiteSpaces,
			out var date)
				? date
				: null;
	}

	private static string? CleanTitle(string? value)
	{
		value = Clean(value);
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = Regex.Replace(
			value,
			@"\s+by\s+.+$",
			string.Empty,
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return value.Trim('-', '|', ':', ' ');
	}

	private static string? CleanDisplayValue(string? value)
	{
		value = Clean(value);
		return string.IsNullOrWhiteSpace(value)
			? null
			: Regex.Replace(value, @"\s+\([\d,]+\)$", string.Empty).Trim();
	}

	private static string? NormalizeUrl(string? value)
	{
		value = Clean(value);
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (value.StartsWith("//"))
			return $"https:{value}";

		return value.StartsWith('/')
			? new Uri(new Uri("https://hentainexus.com/"), value.TrimStart('/')).ToString()
			: value;
	}

	private static string? Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = HtmlEntity.DeEntitize(value);
		value = Regex.Replace(value, @"\s+", " ");
		return value.Trim();
	}

	private static string? First(params string?[] values)
	{
		return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
	}
}
