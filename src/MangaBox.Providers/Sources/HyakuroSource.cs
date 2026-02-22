using System.Threading.RateLimiting;

namespace MangaBox.Providers.Sources;

using Models.Types;
using Utilities.Flare;

public interface IHyakuroSource : IMangaSource
{

}

internal class HyakuroSource(
	IZipService _zip,
	IFlareSolverService _flare,
	ILogger<HyakuroSource> _logger) : IHyakuroSource
{
	private static RateLimiter? _limiter;

	private readonly FlareSolverInstance _flareInstance = _flare.Limiter();

	public string HomeUrl => "https://hyakuro.net";

	public string Provider => "hyakuro";

	public string Name => "Hyakuro";

	public string? Referer => null;

	public string? UserAgent => PolyfillExtensions.USER_AGENT;

	public Dictionary<string, string>? Headers => null;

	public async Task<MangaSource.MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token)
	{
		string zipUrl = $"{HomeUrl}/api/download/{mangaId}/{chapterId}";
		var (error, _, files) = await _zip.DownloadZip(zipUrl, null, token);
		if (!string.IsNullOrEmpty(error) || files.Length == 0)
		{
			_logger.LogError("Failed to download chapter zip from {Url}: {Error}", zipUrl, error);
			return [];
		}

		return [..files.Select(t =>
		{
			var numberPart = Path.GetFileNameWithoutExtension(t);
			var number = double.TryParse(numberPart, out var n) ? n : 0;
			var url = _zip.GenerateImageUrl(zipUrl, t);
			var page = new MangaSource.MangaChapterPage
			{
				Page = url,
				Headers = [new("Raw File name", t)]
			};
			return (number, page);
		}).OrderBy(t => t.number).Select(t => t.page)];
	}

	public RateLimiter GetRateLimiter(string url)
	{
		return _limiter ??= PolyfillExtensions.DefaultRateLimiter();
	}

	public async Task<MangaSource.Manga?> Manga(string id, CancellationToken token)
	{
		var url = id.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) ? id : $"{HomeUrl}/manga/{id}";
		var doc = await _flareInstance.GetHtml(url, token);
		if (doc is null) return null;

		var script = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
		if (script is null) return null;

		var data = JsonSerializer.Deserialize<NextData>(script.InnerText);
		if (data is null) return null;

		var nsfw = data.Props.PageProps.Categories.Contains("adult", StringComparer.InvariantCultureIgnoreCase);

		var tags = data.Props.PageProps.Categories.Where(t => !t.EqualsIc("adult")).ToList();
		if (data.Props.PageProps.Oneshot)
			tags.Add("Oneshot");

		return new()
		{
			Id = id,
			Title = data.Props.PageProps.Title,
			AltTitles = data.Props.PageProps.AlternativeTitles,
			Provider = Provider,
			HomePage = url,
			Cover = data.Props.PageProps.CoverUrl,
			Description = data.Props.PageProps.Info.Synopsis,
			AltDescriptions = [],
			Tags = [..tags],
			Authors = [data.Props.PageProps.Info.Author],
			Artists = [data.Props.PageProps.Info.Artist],
			Rating = nsfw ? ContentRating.Erotica : ContentRating.Safe,
			Nsfw = nsfw,
			Referer = Referer,
			Attributes = [..new MangaSource.MangaAttribute[] 
			{
				new("Added On", data.Props.PageProps.AddedOn),
				new("Updated On", data.Props.PageProps.UpdatedOn),
				new("Status", data.Props.PageProps.Status),
				new("MangaDex Link", data.Props.PageProps.Footer.Mangadex),
				new("MangaUpdates Link", data.Props.PageProps.Footer.Mangaupdates),
				new("Discord Link", data.Props.PageProps.Footer.Discord),
				new("Mail Link", data.Props.PageProps.Footer.Mail),
			}.Where(a => !string.IsNullOrEmpty(a.Value))],
			Chapters = [..data.Props.PageProps.Chapters.Select(c => new MangaSource.MangaChapter
			{
				Title = c.Name,
				Id = c.Number.ToString(),
				Number = c.Number,
				Url = $"{url}/read/{c.Number}/1",
				Attributes = [new MangaSource.MangaAttribute("Date", c.Date)]
			})]
		};
	}

	public (bool matches, string? part) MatchesProvider(string url)
	{
		if (!url.StartsWith(HomeUrl, StringComparison.InvariantCultureIgnoreCase))
			return (false, null);

		var parts = url.Remove(0, HomeUrl.Length)
			.Split('/', StringSplitOptions.RemoveEmptyEntries)
			.Where(t => !t.Equals("manga", StringComparison.InvariantCultureIgnoreCase))
			.ToArray();
		if (parts.Length == 0) return (false, null);

		return (true, parts.First());
	}

	public partial class NextData
	{
		[JsonPropertyName("props")]
		public Props Props { get; set; } = new();

		[JsonPropertyName("page")]
		public string Page { get; set; } = string.Empty;

		[JsonPropertyName("query")]
		public Query Query { get; set; } = new();

		[JsonPropertyName("buildId")]
		public string BuildId { get; set; } = string.Empty;

		[JsonPropertyName("isFallback")]
		public bool IsFallback { get; set; }

		[JsonPropertyName("gsp")]
		public bool Gsp { get; set; }
	}

	public partial class Props
	{
		[JsonPropertyName("pageProps")]
		public PageProps PageProps { get; set; } = new();

		[JsonPropertyName("__N_SSG")]
		public bool NSsg { get; set; }
	}

	public partial class PageProps
	{
		[JsonPropertyName("slug")]
		public string Slug { get; set; } = string.Empty;

		[JsonPropertyName("title")]
		public string Title { get; set; } = string.Empty;

		[JsonPropertyName("oneshot")]
		public bool Oneshot { get; set; }

		[JsonPropertyName("categories")]
		public string[] Categories { get; set; } = [];

		[JsonPropertyName("coverURL")]
		public string CoverUrl { get; set; } = string.Empty;

		[JsonPropertyName("status")]
		public string Status { get; set; } = string.Empty;

		[JsonPropertyName("addedOn")]
		public string AddedOn { get; set; } = string.Empty;

		[JsonPropertyName("updatedOn")]
		public string UpdatedOn { get; set; } = string.Empty;

		[JsonPropertyName("chapters")]
		public ChapterElement[] Chapters { get; set; } = [];

		[JsonPropertyName("alternativeTitles")]
		public string[] AlternativeTitles { get; set; } = [];

		[JsonPropertyName("info")]
		public Info Info { get; set; } = new();

		[JsonPropertyName("footer")]
		public Footer Footer { get; set; } = new();
	}

	public partial class ChapterElement
	{
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[JsonPropertyName("number")]
		public double Number { get; set; }

		[JsonPropertyName("date")]
		public string Date { get; set; } = string.Empty;
	}

	public partial class Footer
	{
		[JsonPropertyName("mangadex")]
		public string Mangadex { get; set; } = string.Empty;

		[JsonPropertyName("mangaupdates")]
		public string Mangaupdates { get; set; } = string.Empty;

		[JsonPropertyName("discord")]
		public string Discord { get; set; } = string.Empty;

		[JsonPropertyName("mail")]
		public string Mail { get; set; } = string.Empty;
	}

	public partial class Info
	{
		[JsonPropertyName("author")]
		public string Author { get; set; } = string.Empty;

		[JsonPropertyName("artist")]
		public string Artist { get; set; } = string.Empty;

		[JsonPropertyName("synopsis")]
		public string Synopsis { get; set; } = string.Empty;
	}

	public partial class Query
	{
		[JsonPropertyName("slug")]
		public string Slug { get; set; } = string.Empty;
	}
}
