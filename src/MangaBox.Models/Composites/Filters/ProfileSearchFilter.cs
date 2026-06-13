namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The search filter for profiles
/// </summary>
public class ProfileSearchFilter : SearchFilter<ProfileOrderBy>
{
	/// <summary>
	/// The providers to filter by
	/// </summary>
	[JsonPropertyName("providers")]
	public string[] Providers { get; set; } = [];

	/// <summary>
	/// The provider IDs to filter by
	/// </summary>
	[JsonPropertyName("providerIds")]
	public string[] ProviderIds { get; set; } = [];

	/// <summary>
	/// Whether or not the profile is an administrator
	/// </summary>
	[JsonPropertyName("admin")]
	public bool? Admin { get; set; }

	/// <summary>
	/// Whether or not the profile is approved to read
	/// </summary>
	[JsonPropertyName("canRead")]
	public bool? CanRead { get; set; }

	/// <summary>
	/// Converts the order by enum to a column name
	/// </summary>
	/// <param name="key">The order key</param>
	/// <returns>The column name</returns>
	public static string OrderColumn(ProfileOrderBy key) => key switch
	{
		ProfileOrderBy.UpdatedAt => "p.updated_at",
		ProfileOrderBy.Username => "p.username",
		ProfileOrderBy.Provider => "p.provider",
		ProfileOrderBy.ProviderId => "p.provider_id",
		ProfileOrderBy.Admin => "p.admin",
		ProfileOrderBy.CanRead => "p.can_read",
		ProfileOrderBy.Random => "RANDOM()",
		_ => "p.created_at"
	};

	/// <summary>
	/// Builds the query to search for profiles
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

		bob.AppendLine($"""
BEGIN;
DROP TABLE IF EXISTS tmp_profile_results_{suffix};

CREATE TEMP TABLE tmp_profile_results_{suffix} ON COMMIT DROP AS
SELECT
	DISTINCT
	p.id,
	{OrderColumn(Order)} as order_column
FROM mb_profiles p
WHERE
	p.deleted_at IS NULL
""");

		if (Ids is { Length: > 0 })
		{
			parameters.Add("ids", Ids.Distinct().ToArray());
			bob.AppendLine("\tAND p.id = ANY( :ids )");
		}

		if (!string.IsNullOrWhiteSpace(Search))
		{
			parameters.Add("search", Search.Trim());
			bob.AppendLine("\tAND p.fts @@ phraseto_tsquery('english', :search)");
		}

		if (Providers is { Length: > 0 })
		{
			parameters.Add("providers", Providers.Select(t => t.ToLower()).Distinct().ToArray());
			bob.AppendLine("\tAND LOWER(p.provider) = ANY( :providers )");
		}

		if (ProviderIds is { Length: > 0 })
		{
			parameters.Add("providerIds", ProviderIds.Distinct().ToArray());
			bob.AppendLine("\tAND p.provider_id = ANY( :providerIds )");
		}

		if (Admin.HasValue)
		{
			parameters.Add("admin", Admin.Value);
			bob.AppendLine("\tAND p.admin = :admin");
		}

		if (CanRead.HasValue)
		{
			parameters.Add("canRead", CanRead.Value);
			bob.AppendLine("\tAND p.can_read = :canRead");
		}

		bob.AppendLine($"""
;
SELECT COUNT(*) FROM tmp_profile_results_{suffix};

SELECT p.*, r.order_column
FROM tmp_profile_results_{suffix} r
JOIN mb_profiles p ON p.id = r.id
ORDER BY r.order_column {(Asc ? "ASC" : "DESC")}
LIMIT :limit OFFSET :offset;

DROP TABLE tmp_profile_results_{suffix};
COMMIT;
""");

		return bob.ToString();
	}
}
