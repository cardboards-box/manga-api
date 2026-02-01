namespace MangaBox.Services;

using MangaBox.Models.Types;
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
	/// Whether or not the URL matches the provider
	/// </summary>
	/// <param name="url">The URL of the manga</param>
	/// <returns>Whether or not it matches and the unique ID of the manga for the source</returns>
	(bool matches, string? part) MatchesProvider(string url);

	/// <summary>
	/// Fetches a manga definition from the given source
	/// </summary>
	/// <param name="id">The ID of the manga to fetch</param>
	/// <returns>The manga or null if something went wrong</returns>
	Task<Manga?> Manga(string id);

	/// <summary>
	/// Fetches the pages for a specific chapter of a manga from the given source
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<MangaChapterPage[]> ChapterPages(string mangaId, string chapterId);
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
	/// <returns>The pages of the chapter or null if something went wrong</returns>
	Task<MangaChapterPage[]> ChapterPages(string url);
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

		public string Title { get; set; } = string.Empty;

		public string Id { get; set; } = string.Empty;

		public string Provider { get; set; } = string.Empty;

		public string HomePage { get; set; } = string.Empty;

		public string Cover { get; set; } = string.Empty;

		public string Description { get; set; } = string.Empty;

		public string[] AltDescriptions { get; set; } = [];

		public string[] AltTitles { get; set; } = [];

		public string[] Tags { get; set; } = [];

		public string[] Authors { get; set; } = [];

		public string[] Artists { get; set; } = [];

		public string[] Uploaders { get; set; } = [];

		public ContentRating Rating { get; set; } = ContentRating.Safe;

		public List<MangaChapter> Chapters { get; set; } = [];

		public bool Nsfw
		{
			get => _nsfw ?? (Rating != ContentRating.Safe);
			set => _nsfw = value;
		}

		public List<MangaAttribute> Attributes { get; set; } = [];

		public string? Referer { get; set; }

		public DateTime? SourceCreated { get; set; }

		public bool OrdinalVolumeReset { get; set; } = false;
	}

	public class MangaAttribute
	{
		public string Name { get; set; } = string.Empty;

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
		public string Title { get; set; } = string.Empty;

		public string Url { get; set; } = string.Empty;

		public string Id { get; set; } = string.Empty;

		public double Number { get; set; }

		public double? Volume { get; set; }

		public string? ExternalUrl { get; set; }

		public List<MangaAttribute> Attributes { get; set; } = [];
	}

	public class MangaChapterPage
	{
		public string Page { get; set; } = string.Empty;

		public int? Width { get; set; }

		public int? Height { get; set; }

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


