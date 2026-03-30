namespace MangaBox.Models.Composites.Filters;

using Types;

/// <summary>
/// The search filter for logs
/// </summary>
public class LogSearchFilter : SearchFilter<LogOrderBy>
{
	/// <summary>
	/// The log levels to filter by
	/// </summary>
	[JsonPropertyName("levels")]
	public MbLogLevel[] Levels { get; set; } = [];

	/// <summary>
	/// The levels to exclude
	/// </summary>
	[JsonPropertyName("levelsEx")]
	public MbLogLevel[] LevelsEx { get; set; } = [];

	/// <summary>
	/// The sources to filter by
	/// </summary>
	[JsonPropertyName("sources")]
	public string[] Sources { get; set; } = [];

	/// <summary>
	/// The sources to exclude
	/// </summary>
	[JsonPropertyName("sourcesEx")]
	public string[] SourcesEx { get; set; } = [];

	/// <summary>
	/// The categories to filter by
	/// </summary>
	[JsonPropertyName("categories")]
	public string[] Categories { get; set; } = [];

	/// <summary>
	/// The categories to exclude
	/// </summary>
	[JsonPropertyName("categoriesEx")]
	public string[] CategoriesEx { get; set; } = [];

	/// <summary>
	/// The request ID to filter by
	/// </summary>
	[JsonPropertyName("requestId")]
	public string? RequestId { get; set; }

	/// <summary>
	/// Ensure the log was created after this date
	/// </summary>
	[JsonPropertyName("after")]
	public DateTime? After { get; set; } = null;

	/// <summary>
	/// Ensure the log was created before this date
	/// </summary>
	[JsonPropertyName("before")]
	public DateTime? Before { get; set; } = null;

	/// <summary>
	/// Converts the order by enum to a column name
	/// </summary>
	/// <param name="key">The order key</param>
	/// <returns>The column name</returns>
	public static string OrderColumn(LogOrderBy key) => key switch
	{
		LogOrderBy.LogLevel => "l.log_level",
		LogOrderBy.Category => "l.category",
		LogOrderBy.Source => "l.source",
		_ => "l.created_at"
	};

	/// <summary>
	/// Builds the query to search for logs
	/// </summary>
	/// <param name="parameters">The dapper parameters</param>
	/// <returns>The query</returns>
	public override string Build(out DynamicParameters parameters)
	{
		parameters = new();
		var bob = new StringBuilder();

		var page = Page <= 0 ? 1 : Page;
		var size = Size <= 0 ? 100 : Size;

		var suffix = TableSuffix();

		parameters.Add("limit", size);
		parameters.Add("offset", (page - 1) * size);
		parameters.Add("profileId", ProfileId);
		bob.AppendLine($"""
BEGIN;
DROP TABLE IF EXISTS tmp_log_results_{suffix};

CREATE TEMP TABLE tmp_log_results_{suffix} ON COMMIT DROP AS
SELECT
	DISTINCT
	l.id,
	{OrderColumn(Order)} as order_column
FROM mb_logs l
WHERE
	l.deleted_at IS NULL 
""");
		if (Ids is { Length: > 0 })
		{
			parameters.Add("ids", Ids);
			bob.AppendLine("\tAND l.id = ANY( :ids )");
		}

		if (!string.IsNullOrWhiteSpace(Search))
		{
			parameters.Add("search", Search.Trim());
			bob.AppendLine("\tAND l.fts @@ phraseto_tsquery('english', :search)");
		}

		if (Levels is {  Length: > 0 })
		{
			parameters.Add("levels", Levels.Select(t => (int)t).ToArray());
			bob.AppendLine("\tAND l.log_level = ANY( :levels )");
		}

		if (LevelsEx is { Length: > 0 })
		{
			parameters.Add("levelsEx", LevelsEx.Select(t => (int)t).ToArray());
			bob.AppendLine("\tAND l.log_level <> ALL( :levelsEx )");
		}

		if (Sources is { Length: > 0 })
		{
			parameters.Add("sources", Sources);
			bob.AppendLine("\tAND l.source = ANY( :sources )");
		}

		if (SourcesEx is { Length: > 0 })
		{
			parameters.Add("sourcesEx", SourcesEx);
			bob.AppendLine("\tAND l.source <> ALL( :sourcesEx )");
		}

		if (Categories is { Length: > 0 })
		{
			parameters.Add("categories", Categories);
			bob.AppendLine("\tAND l.category = ANY( :categories )");
		}

		if (CategoriesEx is { Length: > 0 })
		{
			parameters.Add("categoriesEx", CategoriesEx);
			bob.AppendLine("\tAND l.category <> ALL( :categoriesEx )");
		}

		if (!string.IsNullOrWhiteSpace(RequestId))
		{
			parameters.Add("requestId", RequestId);
			bob.AppendLine("\tAND l.context IS NOT NULL");
			bob.AppendLine("\tAND l.context LIKE '%\"RequestId\":\"' || :requestId || '\"%'");
		}

		if (Before.HasValue)
		{
			parameters.Add("before", Before.Value);
			bob.AppendLine("\tAND l.created_at <= :before");
		}

		if (After.HasValue)
		{
			parameters.Add("after", After.Value);
			bob.AppendLine("\tAND l.created_at >= :after");
		}

		bob.AppendLine($"""
;
SELECT COUNT(*) FROM tmp_log_results_{suffix};

SELECT l.*, r.order_column
FROM tmp_log_results_{suffix} r
JOIN mb_logs l ON l.id = r.id
ORDER BY r.order_column {(Asc ? "ASC" : "DESC")}
LIMIT :limit OFFSET :offset;

DROP TABLE tmp_log_results_{suffix};
COMMIT;
""");
		return bob.ToString();
	}
}
