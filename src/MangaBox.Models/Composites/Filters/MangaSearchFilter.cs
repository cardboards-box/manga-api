namespace MangaBox.Models.Composites.Filters;

using Types;

/// <summary>
/// The search filter for manga
/// </summary>
public class MangaSearchFilter : SearchFilter<MangaOrderBy>
{
	/// <summary>
	/// The ratings to filter by
	/// </summary>
	[JsonPropertyName("ratings")]
	public ContentRating[] Ratings { get; set; } = [];

	/// <summary>
	/// Tags to include in the search
	/// </summary>
	[JsonPropertyName("tags")]
	public Guid[] Tags { get; set; } = [];

	/// <summary>
	/// Tags to exclude in the search
	/// </summary>
	[JsonPropertyName("tagsEx")]
	public Guid[] TagsEx { get; set; } = [];
	
	/// <summary>
	/// All of the sources to fetch manga from
	/// </summary>
	[JsonPropertyName("sources")]
	public Guid[] Sources { get; set; } = [];

	/// <summary>
	/// Whether to match all tags (AND - <see langword="true"/>) or any tag (OR - <see langword="false"/>)
	/// </summary>
	[JsonPropertyName("tagsAnd")]
	public bool TagsAnd { get; set; } = true;

	/// <summary>
	/// Whether to match all exclusion tags (AND - <see langword="true"/>) or any exclusion tag (OR - <see langword="false"/>) 
	/// </summary>
	[JsonPropertyName("tagsExAnd")]
	public bool TagsExAnd { get; set; } = false;

	/// <summary>
	/// The state of the manga in the user's library
	/// </summary>
	[JsonPropertyName("states")]
	public MangaState[] States { get; set; } = [];

	/// <summary>
	/// Whether or not to invert the state filter
	/// </summary>
	[JsonPropertyName("statesInclude")]
	public bool StatesInclude { get; set; } = true;

	/// <summary>
	/// Whether or not to use AND for the states filter
	/// </summary>
	[JsonPropertyName("statesAnd")]
	public bool StatesAnd { get; set; } = true;

	/// <summary>
	/// The minimum number of chapters the manga should have
	/// </summary>
	[JsonPropertyName("chapMin")]
	public int? ChapMin { get; set; } = null;

	/// <summary>
	/// The maximum number of chapters the manga should have
	/// </summary>
	[JsonPropertyName("chapMax")]
	public int? ChapMax { get; set; } = null;

	/// <summary>
	/// Ensure the manga was created after this date
	/// </summary>
	[JsonPropertyName("mAfter")]
	public DateTime? MAfter { get; set; } = null;

	/// <summary>
	/// Ensure the manga was created before this date
	/// </summary>
	[JsonPropertyName("mBefore")]
	public DateTime? MBefore { get; set; } = null;

	/// <summary>
	/// Ensure the latest chapter was created after this date
	/// </summary>
	[JsonPropertyName("cLastAfter")]
	public DateTime? CLastAfter { get; set; } = null;

	/// <summary>
	/// Ensure the latest chapter was created before this date
	/// </summary>
	[JsonPropertyName("cLastBefore")]
	public DateTime? CLastBefore { get; set; } = null;

	/// <summary>
	/// Ensure the first chapter was created after this date
	/// </summary>
	[JsonPropertyName("cFirstAfter")]
	public DateTime? CFirstAfter { get; set; } = null;

	/// <summary>
	/// Ensure the first chapter was created before this date
	/// </summary>
	[JsonPropertyName("cFirstBefore")]
	public DateTime? CFirstBefore { get; set; } = null;

	/// <summary>
	/// Converts the order by enum to a column name
	/// </summary>
	/// <param name="key">The order key</param>
	/// <returns>The column name</returns>
	public static string OrderColumn(MangaOrderBy key) => key switch
	{
		MangaOrderBy.MangaCreatedAt => "m.created_at",
		MangaOrderBy.MangaUpdatedAt => "m.updated_at",
		MangaOrderBy.LastChapterCreatedAt => "ext.last_chapter_created",
		MangaOrderBy.LastChapterUpdatedAt => "last_ch.updated_at",
		MangaOrderBy.FirstChapterCreatedAt => "ext.first_chapter_created",
		MangaOrderBy.FirstChapterUpdatedAt => "first_ch.updated_at",
		MangaOrderBy.ChapterCount => "ext.unique_chapter_count",
		MangaOrderBy.VolumeCount => "ext.volume_count",
		MangaOrderBy.Views => "ext.views",
		MangaOrderBy.Favorites => "ext.favorites",
		MangaOrderBy.Title => "COALESCE(ext.display_title, m.title)",
		MangaOrderBy.LastRead => "mp.last_read_at",
		MangaOrderBy.Random => "RANDOM()",
		_ => "COALESCE(ext.display_title, m.title)"
	};

	/// <summary>
	/// Builds the query to search for manga
	/// </summary>
	/// <param name="parameters">The dapper parameters</param>
	/// <returns>The query</returns>
	public override string Build(out DynamicParameters parameters)
	{
		void HandleTags(StringBuilder bob, DynamicParameters parameters)
		{
			if (Tags is not { Length: > 0 }) return;

			parameters.Add("tags", Tags);

			if (!TagsAnd)
			{
				bob.AppendLine("""
	AND EXISTS (
		SELECT 1
		FROM mb_manga_tags mt
		WHERE mt.manga_id = m.id
		  AND mt.tag_id = ANY( :tags )
	)
""");
				return;
			}

			parameters.Add("tagCount", Tags.Length);

			bob.AppendLine("""
	AND m.id IN (
		SELECT mt.manga_id
		FROM mb_manga_tags mt
		WHERE mt.tag_id = ANY( :tags )
		GROUP BY mt.manga_id
		HAVING COUNT(DISTINCT mt.tag_id) = :tagCount
	)
""");
		}

		void HandleTagsEx(StringBuilder bob, DynamicParameters parameters)
		{
			if (TagsEx is not { Length: > 0 }) return;

			parameters.Add("tagsEx", TagsEx);
			if (!TagsExAnd)
			{
				bob.AppendLine("""
  AND NOT EXISTS (
		SELECT 1
		FROM mb_manga_tags mt
		WHERE mt.manga_id = m.id
		  AND mt.tag_id = ANY( :tagsEx )
  )
""");
				return;
			}

			parameters.Add("tagExCount", TagsEx.Length);
			bob.AppendLine("""
  AND m.id NOT IN (
		SELECT mt.manga_id
		FROM mb_manga_tags mt
		WHERE mt.tag_id = ANY( :tagsEx )
		GROUP BY mt.manga_id
		HAVING COUNT(DISTINCT mt.tag_id) = :tagExCount
  )
""");
		}

		void HandleStates(StringBuilder bob, DynamicParameters parameters)
		{
			if (States is not { Length: > 0 } || !ProfileId.HasValue)
				return;

			var conditions = new List<string>();

			if (States.Contains(MangaState.Favorited))
			{
				if (StatesInclude) conditions.Add("(mp.favorited IS NOT NULL AND mp.favorited = TRUE)");
				else conditions.Add("(mp.favorited IS NULL OR mp.favorited = FALSE)");
			}

			if (States.Contains(MangaState.Completed))
			{
				if (StatesInclude) conditions.Add("(mp.is_completed IS NOT NULL AND mp.is_completed = TRUE)");
				else conditions.Add("(mp.is_completed IS NULL OR mp.is_completed = FALSE)");
			}

			if (States.Contains(MangaState.InProgress))
			{
				if (StatesInclude) conditions.Add(@"(mp.last_read_chapter_id IS NOT NULL AND mp.is_completed IS NOT NULL AND mp.is_completed = FALSE)");
				else conditions.Add("(mp.last_read_chapter_id IS NULL OR mp.is_completed IS NULL OR mp.is_completed = FALSE)");
			}

			if (States.Contains(MangaState.Bookmarked))
			{
				conditions.Add($"""
{(!StatesInclude ? "NOT " : "")}EXISTS (
			SELECT 1
			FROM mb_chapter_progress cp
			JOIN mb_chapters ch ON ch.id = cp.chapter_id
			WHERE 
				cp.progress_id = mp.id AND 
				cp.deleted_at IS NULL AND 
				ch.deleted_at IS NULL AND 
				ch.manga_id = m.id AND 
				COALESCE(array_length(cp.bookmarks, 1), 0) > 0
		)
""");
			}

			if (conditions.Count == 0)
				return;

			var conjunction = StatesAnd ? "\n\t\tAND " : "\n\t\tOR ";
			bob.AppendLine("\tAND (");
			bob.AppendLine("\t\t" + string.Join(conjunction, conditions));
			bob.AppendLine("\t)");
		}

		void HandleDates(StringBuilder bob, DynamicParameters parameters)
		{
			if (MAfter.HasValue)
			{
				parameters.Add("mAfter", MAfter.Value);
				bob.AppendLine("\tAND m.created_at >= :mAfter");
			}

			if (MBefore.HasValue)
			{
				parameters.Add("mBefore", MBefore.Value);
				bob.AppendLine("\tAND m.created_at <= :mBefore");
			}

			if (CLastAfter.HasValue)
			{
				parameters.Add("cLastAfter", CLastAfter.Value);
				bob.AppendLine("\tAND ext.last_chapter_created >= :cLastAfter");
			}

			if (CLastBefore.HasValue)
			{
				parameters.Add("cLastBefore", CLastBefore.Value);
				bob.AppendLine("\tAND ext.last_chapter_created <= :cLastBefore");
			}

			if (CFirstAfter.HasValue)
			{
				parameters.Add("cFirstAfter", CFirstAfter.Value);
				bob.AppendLine("\tAND ext.first_chapter_created >= :cFirstAfter");
			}

			if (CFirstBefore.HasValue)
			{
				parameters.Add("cFirstBefore", CFirstBefore.Value);
				bob.AppendLine("\tAND ext.first_chapter_created <= :cFirstBefore");
			}
		}

		parameters = new DynamicParameters();
		var bob = new StringBuilder();
		
		var page = Page <= 0 ? 1 : Page;
		var size = Size <= 0 ? 100 : Size;

		var suffix = TableSuffix();

		parameters.Add("limit", size);
		parameters.Add("offset", (page - 1) * size);

		bob.AppendLine($"""
BEGIN;
DROP TABLE IF EXISTS tmp_manga_results_{suffix};

CREATE TEMP TABLE tmp_manga_results_{suffix} ON COMMIT DROP AS
SELECT
	m.id,
	{OrderColumn(Order)} as order_column
FROM mb_manga m
JOIN mb_manga_ext ext ON ext.manga_id = m.id
LEFT JOIN mb_chapters last_ch ON last_ch.id = ext.last_chapter_id
LEFT JOIN mb_chapters first_ch ON first_ch.id = ext.first_chapter_id
""");
		if (ProfileId.HasValue)
		{
			parameters.Add("profileId", ProfileId.Value);
			bob.AppendLine(@"LEFT JOIN mb_manga_progress mp ON mp.manga_id = m.id
	AND mp.profile_id = :profileId
	AND mp.deleted_at IS NULL");
		}

		bob.AppendLine("""
WHERE 
	m.deleted_at IS NULL
	AND ext.deleted_at IS NULL
""");

		if (Ids is { Length: > 0 })
		{
			parameters.Add("ids", Ids);
			bob.AppendLine("\tAND m.id = ANY( :ids )");
		}

		if (!string.IsNullOrWhiteSpace(Search))
		{
			parameters.Add("search", Search.Trim());
			bob.AppendLine("\tAND m.fts @@ phraseto_tsquery('english', :search)");
		}

		if (Ratings is { Length: > 0 })
		{
			parameters.Add("ratings", Ratings.Select(t => (int)t).ToArray());
			bob.AppendLine("\tAND m.content_rating = ANY( :ratings )");
		}

		if (Sources is { Length: > 0 })
		{
			parameters.Add("sources", Sources);
			bob.AppendLine("\tAND m.source_id = ANY( :sources )");
		}

		if (ChapMin.HasValue)
		{
			parameters.Add("chapMin", ChapMin.Value);
			bob.AppendLine("\tAND ext.unique_chapter_count >= :chapMin");
		}

		if (ChapMax.HasValue)
		{
			parameters.Add("chapMax", ChapMax.Value);
			bob.AppendLine("\tAND ext.unique_chapter_count <= :chapMax");
		}

		HandleDates(bob, parameters);
		HandleTags(bob, parameters);
		HandleTagsEx(bob, parameters);
		HandleStates(bob, parameters);
		bob.AppendLine($"""
;
SELECT COUNT(*) FROM tmp_manga_results_{suffix};

CREATE TEMP TABLE tmp_manga_results_{suffix}_ordered ON COMMIT DROP AS
SELECT id, order_column
FROM tmp_manga_results_{suffix}
ORDER BY order_column {(Asc ? "ASC" : "DESC")}
LIMIT :limit OFFSET :offset;

DROP TABLE tmp_manga_results_{suffix};

SELECT i.*
FROM mb_images i
JOIN tmp_manga_results_{suffix}_ordered p ON p.id = i.manga_id
WHERE i.chapter_id IS NULL AND i.deleted_at IS NULL;

SELECT e.*
FROM mb_manga_ext e
JOIN tmp_manga_results_{suffix}_ordered p ON p.id = e.manga_id
WHERE e.deleted_at IS NULL;

SELECT DISTINCT s.*
FROM mb_sources s
JOIN mb_manga m ON m.source_id = s.id
JOIN tmp_manga_results_{suffix}_ordered p ON p.id = m.id
WHERE 
	m.deleted_at IS NULL AND
	s.deleted_at IS NULL;

SELECT DISTINCT t.*
FROM mb_tags t 
JOIN mb_manga_tags mt ON mt.tag_id = t.id
JOIN tmp_manga_results_{suffix}_ordered p ON p.id = mt.manga_id
WHERE t.deleted_at IS NULL AND mt.deleted_at IS NULL;

SELECT DISTINCT mt.manga_id, mt.tag_id
FROM mb_manga_tags mt
JOIN tmp_manga_results_{suffix}_ordered p ON p.id = mt.manga_id
WHERE mt.deleted_at IS NULL;

SELECT m.*, t.order_column
FROM mb_manga m
JOIN tmp_manga_results_{suffix}_ordered t ON m.id = t.id
ORDER BY t.order_column {(Asc ? "ASC" : "DESC")};

DROP TABLE tmp_manga_results_{suffix}_ordered;
COMMIT;
""");
		return bob.ToString();
	}
}