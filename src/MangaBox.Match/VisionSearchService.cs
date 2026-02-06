using F23.StringSimilarity;
using Google.Cloud.Vision.V1;
using HtmlAgilityPack;
using MangaDexSharp;
using Image = Google.Cloud.Vision.V1.Image;

namespace MangaBox.Match;

using TaskType = Func<Task<Image>>;

internal class VisionSearchService(
	IDbService _db,
	IMangaDex _api,
	IMangaLoaderService _loader,
	ILogger<VisionSearchService> _logger) : IImageSearchService
{
	public const string SERVICE_SLUG = "google-vision";
	public const int MAX_MD_RESULTS = 3;

	public RISServices Type => RISServices.GoogleVision;

	public IAsyncEnumerable<ImageSearchResult> Search(string url, CancellationToken token)
	{
		return Search(() => Task.FromResult(Image.FromUri(url)), url, token);
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(MemoryStream stream, string fileName, 
		[EnumeratorCancellation] CancellationToken token)
	{
		stream.Position = 0;
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms, token);
		ms.Position = 0;
		await foreach (var result in Search(() => Image.FromStreamAsync(ms), fileName, token))
			yield return result;
	}

	public static string PurgeTitle(string title)
	{
		var regexPurgers = new[]
		{
			("manga", "manga[a-z]{1,}\\b")
		};

		var purgers = new[]
		{
			("chapter", new[] { "chapter" }),
			("chap", ["chap"]),
			("read", ["read"]),
			("online", ["online"]),
			("manga", ["manga"]),
			("season", ["season"]),
			("facebook", ["facebook"])
		};

		title = title.ToLower();

		if (title.Contains('<'))
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(title);
			title = doc.DocumentNode.InnerText;
		}

		if (title.Contains('&')) 
			title = WebUtility.HtmlDecode(title);

		foreach (var (text, regex) in regexPurgers)
			if (title.Contains(text))
				title = Regex.Replace(title, regex, string.Empty);

		foreach (var (text, replacers) in purgers)
			if (title.Contains(text))
				foreach (var regex in replacers)
					title = title.Replace(regex, "").Trim();

		title = new string([.. title
			.Select(t => !char.IsPunctuation(t) &&
				!char.IsNumber(t) &&
				!char.IsSymbol(t) ? t : ' ')]);

		while (title.Contains("  "))
			title = title.Replace("  ", " ").Trim();

		return title;
	}

	public async Task<MangaBoxType<MbManga>?> FindMangaByTitle(string title)
	{
		var results = await _db.Manga.Search(new()
		{
			Search = title,
			Page = 1,
			Size = 10
		});

		var check = new NormalizedLevenshtein();
		return results.Results.OrderByDescending(t =>
		{
			var mt = PurgeTitle(t.Entity.Title);
			return check.Distance(title, mt);
		}).FirstOrDefault();
	}

	public async Task<MangaBoxType<MbManga>?> FindMangaByMd(string title, CancellationToken token)
	{
		var filter = new MangaFilter
		{
			Title = title,
			Order = new()
			{
				[MangaFilter.OrderKey.relevance] = OrderValue.desc
			},
			Limit = 5
		};
		var result = (await _api.Manga.List(filter))?
			.Data?.FirstOrDefault();
		if (result is null) return null;

		var load = await _loader.Load(null, $"https://mangadex.org/title/{result.Id}", false, token);
		if (load is null || !load.Success || 
			load is not Boxed<MangaBoxType<MbManga>> boxed ||
			boxed.Data is null)
			return null;

		return boxed.Data;
	}

	public async IAsyncEnumerable<ImageSearchResult> Search(TaskType request, string context, 
		[EnumeratorCancellation] CancellationToken token)
	{
		WebDetection? detection;
		try
		{
			var image = await request();
			var client = await ImageAnnotatorClient.CreateAsync(token);
			detection = await client.DetectWebInformationAsync(image);
			if (detection is null)
				_logger.LogWarning("No web detection results for {Context}", context);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during Vision API search for {Context}", context);
			detection = null;
		}

		if (detection?.PagesWithMatchingImages is null ||
			detection.PagesWithMatchingImages.Count == 0) 
			yield break;

		int mdResults = 0;
		foreach(var page in detection.PagesWithMatchingImages)
		{
			var image = page.FullMatchingImages.OrderByDescending(t => t.Score).FirstOrDefault()?.Url
				?? page.PartialMatchingImages.OrderByDescending(t => t.Score).FirstOrDefault()?.Url;
			var title = PurgeTitle(page.PageTitle ?? string.Empty);
			var score = page.FullMatchingImages.Concat(page.PartialMatchingImages)
				.OrderByDescending(t => t.Score)
				.FirstOrDefault()?.Score ?? page.Score;
			if (score < 0) score *= 100;

			ImageSearchResult result = new()
			{
				Source = SERVICE_SLUG,
				Image = image,
				Score = score,
				Exact = score >= 99,
				Result = VisionPage.Convert(page),
				Closest = await FindMangaByTitle(title)
			};

			if (result.Closest is not null)
			{
				yield return result;
				continue;
			}

			if (mdResults >= MAX_MD_RESULTS)
				continue;

			mdResults++;
			result.Closest = await FindMangaByMd(title, token);
			yield return result;
		}
	}
}


/// <summary>
/// A image url and it's score
/// </summary>
/// <param name="Score">The match score</param>
/// <param name="Url">The image url</param>
public record class VisionItem(
	[property: JsonPropertyName("score")] float Score,
	[property: JsonPropertyName("url")] string Url);

/// <summary>
/// Represents a matching web page
/// </summary>
/// <param name="FullMatches">Any fully matching images</param>
/// <param name="PartialMatches">Any partially matching images</param>
/// <param name="Url">The page URL</param>
/// <param name="Score">The score</param>
/// <param name="Title">The page title</param>
/// <param name="PurgeTitle">The purged page title</param>
[InterfaceOption(VisionSearchService.SERVICE_SLUG)]
public record class VisionPage(
	[property: JsonPropertyName("fullMatches")] VisionItem[] FullMatches,
	[property: JsonPropertyName("partialMatches")] VisionItem[] PartialMatches,
	[property: JsonPropertyName("url")] string Url,
	[property: JsonPropertyName("score")] float Score,
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("purgeTitle")] string PurgeTitle) : IImageSearchResult
{
	internal static VisionPage Convert(WebDetection.Types.WebPage page)
	{
		var full = page.FullMatchingImages.Select(i => new VisionItem(i.Score, i.Url)).ToArray();
		var partial = page.PartialMatchingImages.Select(i => new VisionItem(i.Score, i.Url)).ToArray();
		return new VisionPage(full, partial,
			page.Url, page.Score, page.PageTitle,
			VisionSearchService.PurgeTitle(page.PageTitle));
	}
}