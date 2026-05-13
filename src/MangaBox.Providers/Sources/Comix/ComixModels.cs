using MangaBox.Utilities.Flare.Models;

namespace MangaBox.Providers.Sources.Comix;

public class Comix
{
	public partial class Manga
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }

		[JsonPropertyName("hid")]
		public string Hid { get; set; } = string.Empty;

		[JsonIgnore]
		public string HashId => Hid;

		[JsonPropertyName("title")]
		public string Title { get; set; } = string.Empty;

		[JsonIgnore]
		public string Slug
		{
			get
			{
				if (string.IsNullOrWhiteSpace(Url))
					return string.Empty;

				var path = new Uri(Url).AbsolutePath.Trim('/');
				var titleIndex = path.IndexOf("title/", StringComparison.OrdinalIgnoreCase);
				if (titleIndex >= 0)
					path = path[(titleIndex + 6)..];

				var slashIndex = path.IndexOf('/');
				if (slashIndex >= 0)
					path = path[..slashIndex];

				var dashIndex = path.IndexOf('-');
				if (dashIndex >= 0)
					path = path[(dashIndex + 1)..];

				return path;
			}
		}

		[JsonPropertyName("type")]
		public string Type { get; set; } = string.Empty;

		[JsonPropertyName("status")]
		public string Status { get; set; } = string.Empty;

		[JsonPropertyName("originalLanguage")]
		public string OriginalLanguage { get; set; } = string.Empty;

		[JsonPropertyName("poster")]
		public Poster Poster { get; set; } = new();

		[JsonPropertyName("latestChapter")]
		public double LatestChapter { get; set; }

		[JsonPropertyName("finalChapter")]
		public double FinalChapter { get; set; }

		[JsonPropertyName("finalVolume")]
		public double FinalVolume { get; set; }

		[JsonPropertyName("hasChapters")]
		public bool HasChapters { get; set; }

		[JsonPropertyName("chapterUpdatedAtFormatted")]
		public string ChapterUpdatedAtFormatted { get; set; } = string.Empty;

		[JsonPropertyName("createdAtFormatted")]
		public string CreatedAtFormatted { get; set; } = string.Empty;

		[JsonPropertyName("updatedAtFormatted")]
		public string UpdatedAtFormatted { get; set; } = string.Empty;

		[JsonPropertyName("startDate")]
		public string StartDate { get; set; } = string.Empty;

		[JsonPropertyName("endDate")]
		public string EndDate { get; set; } = string.Empty;

		[JsonPropertyName("year")]
		public long Year { get; set; }

		[JsonPropertyName("rank")]
		public long Rank { get; set; }

		[JsonPropertyName("synopsis")]
		public string Synopsis { get; set; } = string.Empty;

		[JsonPropertyName("synopsisHtml")]
		public string SynopsisHtml { get; set; } = string.Empty;

		[JsonPropertyName("followsTotal")]
		public long FollowsTotal { get; set; }

		[JsonPropertyName("ratedAvg")]
		public double RatedAvg { get; set; }

		[JsonPropertyName("ratedCount")]
		public long RatedCount { get; set; }

		[JsonPropertyName("contentRating")]
		public string ContentRating { get; set; } = string.Empty;

		[JsonIgnore]
		public bool IsNsfw => !string.IsNullOrWhiteSpace(ContentRating) && !string.Equals(ContentRating, "safe", StringComparison.OrdinalIgnoreCase);

		[JsonIgnore]
		public long CreatedAt => 0;

		[JsonPropertyName("links")]
		public Dictionary<string, string> Links { get; set; } = [];

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;

		[JsonPropertyName("uploadUrl")]
		public string UploadUrl { get; set; } = string.Empty;

		[JsonPropertyName("editUrl")]
		public string EditUrl { get; set; } = string.Empty;

		[JsonPropertyName("altTitles")]
		public string[] AltTitles { get; set; } = [];

		[JsonPropertyName("genres")]
		public ComixNamedItem[] Genres { get; set; } = [];

		[JsonPropertyName("demographics")]
		public ComixNamedItem[] Demographics { get; set; } = [];

		[JsonPropertyName("formats")]
		public ComixNamedItem[] Formats { get; set; } = [];

		[JsonPropertyName("tags")]
		public ComixNamedItem[] Tags { get; set; } = [];

		[JsonPropertyName("authors")]
		public ComixNamedItem[] Authors { get; set; } = [];

		[JsonPropertyName("artists")]
		public ComixNamedItem[] Artists { get; set; } = [];

		[JsonPropertyName("publishers")]
		public ComixNamedItem[] Publishers { get; set; } = [];

		[JsonPropertyName("sources")]
		public ComixNamedItem[] Sources { get; set; } = [];

		[JsonPropertyName("firstChapterUrl")]
		public string FirstChapterUrl { get; set; } = string.Empty;

		[JsonPropertyName("latestChapterUrl")]
		public string LatestChapterUrl { get; set; } = string.Empty;
	}

	public partial class Poster
	{
		[JsonPropertyName("medium")]
		public string Medium { get; set; } = string.Empty;

		[JsonPropertyName("large")]
		public string Large { get; set; } = string.Empty;
	}

	public partial class ComixNamedItem
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }

		[JsonPropertyName("title")]
		public string Title { get; set; } = string.Empty;

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("slug")]
		public string Slug { get; set; } = string.Empty;
	}

	public partial class Chapter
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }

		[JsonIgnore]
		public long ChapterId => Id;

		[JsonPropertyName("mangaId")]
		public long MangaId { get; set; }

		[JsonPropertyName("number")]
		public double Number { get; set; }

		[JsonPropertyName("volume")]
		public double Volume { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("language")]
		public string Language { get; set; } = string.Empty;

		[JsonPropertyName("isOfficial")]
		public bool IsOfficial { get; set; }

		[JsonPropertyName("votes")]
		public long Votes { get; set; }

		[JsonPropertyName("createdAtFormatted")]
		public string CreatedAtFormatted { get; set; } = string.Empty;

		[JsonPropertyName("group")]
		public ComixNamedItem? Group { get; set; }

		[JsonIgnore]
		public ComixNamedItem? ScanlationGroup
		{
			get => Group;
			set => Group = value;
		}

		[JsonPropertyName("creator")]
		public ComixNamedItem? Creator { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;

		[JsonIgnore]
		public SolverSolution? Solver { get; set; }
	}

	public class ChapterList
	{
		[JsonPropertyName("items")]
		public Chapter[] Items { get; set; } = [];

		[JsonPropertyName("meta")]
		public ChapterMeta Meta { get; set; } = new();

		[JsonIgnore]
		public ChapterMeta Pagination
		{
			get => Meta;
			set => Meta = value;
		}
	}

	public partial class ChapterMeta
	{
		[JsonPropertyName("total")]
		public long Total { get; set; }

		[JsonPropertyName("perPage")]
		public long PerPage { get; set; }

		[JsonPropertyName("page")]
		public long Page { get; set; }

		[JsonPropertyName("lastPage")]
		public long LastPage { get; set; }

		[JsonPropertyName("from")]
		public long From { get; set; }

		[JsonPropertyName("to")]
		public long To { get; set; }
	}

	public partial class ChapterDetail
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }

		[JsonPropertyName("mangaId")]
		public long MangaId { get; set; }

		[JsonPropertyName("number")]
		public double Number { get; set; }

		[JsonPropertyName("volume")]
		public double Volume { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("language")]
		public string Language { get; set; } = string.Empty;

		[JsonPropertyName("isOfficial")]
		public bool IsOfficial { get; set; }

		[JsonPropertyName("votes")]
		public long Votes { get; set; }

		[JsonPropertyName("createdAtFormatted")]
		public string CreatedAtFormatted { get; set; } = string.Empty;

		[JsonPropertyName("group")]
		public ComixNamedItem? Group { get; set; }

		[JsonPropertyName("creator")]
		public ComixNamedItem? Creator { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;

		[JsonPropertyName("pages")]
		public ChapterPages Pages { get; set; } = new();

		[JsonPropertyName("prev")]
		public ChapterLink? Prev { get; set; }

		[JsonPropertyName("next")]
		public ChapterLink? Next { get; set; }
	}

	public partial class ChapterPages
	{
		[JsonPropertyName("baseUrl")]
		public string BaseUrl { get; set; } = string.Empty;

		[JsonPropertyName("items")]
		public ChapterPage[] Items { get; set; } = [];
	}

	public partial class ChapterPage
	{
		[JsonPropertyName("width")]
		public long Width { get; set; }

		[JsonPropertyName("height")]
		public long Height { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;
	}

	public partial class ChapterLink
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }

		[JsonPropertyName("number")]
		public double Number { get; set; }

		[JsonPropertyName("volume")]
		public double Volume { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;
	}

}

public class Comix<T>
{
	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("result")]
	public T Result { get; set; } = default!;

	[JsonIgnore]
	public SolverSolution? Solver { get; set; }
}

public class ComixEncryptedResponse
{
	[JsonPropertyName("e")]
	public string EncryptedPayload { get; set; } = string.Empty;
}

public class ComixErrorResponse
{
	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("code")]
	public int Code { get; set; }
}
