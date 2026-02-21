using System.Threading.RateLimiting;

namespace MangaBox.Services;

using Models.Types;
using static MangaSource;

/// <summary>
/// Represents a service that provides access to a third party manga source
/// </summary>
public interface IMangaSource
{
	/// <summary>
	/// The home URL of the manga source
	/// </summary>
	string HomeUrl { get; }

	/// <summary>
	/// The provider slug for the source
	/// </summary>
	string Provider { get; }

	/// <summary>
	/// The display name of the manga source
	/// </summary>
	string Name { get; }

	/// <summary>
	/// The referer to add as a header when making image requests
	/// </summary>
	string? Referer { get; }

	/// <summary>
	/// The user-agent to add when making image requests
	/// </summary>
	string? UserAgent { get; }

	/// <summary>
	/// The headers to add when making image requests
	/// </summary>
	public Dictionary<string, string>? Headers { get; }

	/// <summary>
	/// Whether or not the URL matches the provider
	/// </summary>
	/// <param name="url">The URL of the manga</param>
	/// <returns>Whether or not it matches and the unique ID of the manga for the source</returns>
	(bool matches, string? part) MatchesProvider(string url);

	/// <summary>
	/// Fetches a manga definition from the given source
	/// </summary>
	/// <param name="id">The ID of the manga to fetch</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The manga or null if something went wrong</returns>
	Task<Manga?> Manga(string id, CancellationToken token);

	/// <summary>
	/// Fetches the pages for a specific chapter of a manga from the given source
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token);

	/// <summary>
	/// Gets a rate limiter for fetching images from the source
	/// </summary>
	/// <param name="url">The URL of the image being fetched</param>
	/// <returns>The rate limiter to use for fetching images</returns>
	RateLimiter GetRateLimiter(string url);
}

/// <summary>
/// An alternative manga source interface that uses URLs instead of IDs
/// </summary>
public interface IMangaUrlSource : IMangaSource
{
	/// <summary>
	/// Fetches the page for a specific chapter of a manga from the given source
	/// </summary>
	/// <param name="url">The URL of the page the chapters are on</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<MangaChapterPage[]> ChapterPages(string url, CancellationToken token);
}

/// <summary>
/// Represents a manga source that can be scanned for new manga to add to the database
/// </summary>
public interface IIndexableMangaSource : IMangaSource
{
	/// <summary>
	/// Triggers the indexing process for the source
	/// </summary>
	/// <param name="source">The source being indexed</param>
	/// <param name="token">The token for when to stop processing</param>
	/// <returns>The updated manga</returns>
	IAsyncEnumerable<Manga> Index(LoaderSource source, CancellationToken token);
}

/// <summary>
/// Represents a manga source that has a default content rating
/// </summary>
public interface IRatedSource
{
	/// <summary>
	/// The rating to apply
	/// </summary>
	ContentRating DefaultRating { get; }
}

/// <summary>
/// A class scoping for manga source classes
/// </summary>
/// <remarks>TODO: Rename these later and find a proper home for them</remarks>
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class MangaSource
{
	public class Manga
	{
		private bool? _nsfw = null;

		[JsonPropertyName("title")]
		public string Title { get; set; } = string.Empty;

		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("provider")]
		public string Provider { get; set; } = string.Empty;

		[JsonPropertyName("homePage")]
		public string HomePage { get; set; } = string.Empty;

		[JsonPropertyName("cover")]
		public string Cover { get; set; } = string.Empty;

		[JsonPropertyName("description")]
		public string? Description { get; set; } = string.Empty;

		[JsonPropertyName("altDescriptions")]
		public string[] AltDescriptions { get; set; } = [];

		[JsonPropertyName("altTitles")]
		public string[] AltTitles { get; set; } = [];

		[JsonIgnore]
		public string[] Tags { get; set; } = [];

		[JsonPropertyName("authors")]
		public string[] Authors { get; set; } = [];

		[JsonPropertyName("artists")]
		public string[] Artists { get; set; } = [];

		[JsonPropertyName("uploaders")]
		public string[] Uploaders { get; set; } = [];

		[JsonPropertyName("rating")]
		public ContentRating? Rating { get; set; } = null;

		[JsonPropertyName("chapters")]
		public List<MangaChapter> Chapters { get; set; } = [];

		[JsonPropertyName("nsfw")]
		public bool? Nsfw
		{
			get => _nsfw ?? (Rating is null ? null : (Rating != ContentRating.Safe));
			set => _nsfw = value;
		}
		
		[JsonPropertyName("attributes")]
		public List<MangaAttribute> Attributes { get; set; } = [];

		[JsonPropertyName("tags")]
		public MangaTag[] MangaTags
		{
			get => [..Tags.Select(t => new MangaTag { Name = t }).DistinctBy(t => t.Slug)];
			set => Tags = [..value.Select(t => t.Name).Distinct()];
		}
		
		[JsonPropertyName("referer")]
		public string? Referer { get; set; }

		[JsonPropertyName("sourceCreated")]
		public DateTime? SourceCreated { get; set; }

		[JsonPropertyName("ordinalVolumeReset")]
		public bool OrdinalVolumeReset { get; set; } = false;

		[JsonPropertyName("legacyId")]
		public int? LegacyId { get; set; }
	}

	public class MangaTag
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("slug")]
		public string Slug
		{
			get => field ??= MbTag.GenerateSlug(Name);
			set => field = MbTag.GenerateSlug(value);
		}
	}

	public class MangaAttribute
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("value")]
		public string Value { get; set; } = string.Empty;

		public MangaAttribute() { }

		public MangaAttribute(string name, string value)
		{
			Name = name;
			Value = value;
		}
	}

	public class MangaChapter
	{
		[JsonPropertyName("title")]
		public string? Title { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;

		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("number")]
		public double Number { get; set; }

		[JsonPropertyName("volume")]
		public double? Volume { get; set; }

		[JsonPropertyName("externalUrl")]
		public string? ExternalUrl { get; set; }

		[JsonPropertyName("language")]
		public string? Langauge { get; set; }

		[JsonPropertyName("attributes")]
		public List<MangaAttribute> Attributes { get; set; } = [];

		[JsonPropertyName("legacyId")]
		public int? LegacyId { get; set; }

		/// <summary>
		/// The optional pages for the chapter
		/// </summary>
		[JsonPropertyName("pages")]
		public List<MangaChapterPage> Pages { get; set; } = [];
	}

	public class MangaChapterPage
	{
		[JsonPropertyName("page")]
		public string Page { get; set; } = string.Empty;

		[JsonPropertyName("width")]
		public int? Width { get; set; }

		[JsonPropertyName("height")]
		public int? Height { get; set; }

		[JsonPropertyName("headers")]
		public List<MangaAttribute> Headers { get; set; } = [];

		public MangaChapterPage() { }

		public MangaChapterPage(string page, int? width = null, int? height = null)
		{
			Page = page;
			Width = width;
			Height = height;
		}
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member


