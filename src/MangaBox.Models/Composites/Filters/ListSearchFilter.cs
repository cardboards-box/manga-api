namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The search filter for lists
/// </summary>
public class ListSearchFilter : SearchFilter<ListOrderBy>
{
	/// <summary>
	/// The types of lists to search for
	/// </summary>
	[JsonPropertyName("types")]
	public ListType[] Types { get; set; } = [];

	/// <summary>
	/// Converts the order by enum to a column name
	/// </summary>
	/// <param name="key">The order key</param>
	/// <returns>The column name</returns>
	public static string OrderColumn(ListOrderBy key) => key switch
	{
		ListOrderBy.CreatedAt => "l.created_at",
		ListOrderBy.UpdatedAt => "l.updated_at",
		ListOrderBy.Random => "RANDOM()",
		ListOrderBy.IsPublic => "l.is_public",
		_ => "l.name"
	};

	/// <summary>
	/// Builds the query to search for lists
	/// </summary>
	/// <param name="parameters">The dapper parameters</param>
	/// <returns>The query</returns>
	public override string Build(out DynamicParameters parameters)
	{
		void HandleTypes(StringBuilder bob, DynamicParameters parameters)
		{
			var types = Types.Length > 0 ? Types : [.. ListType.Mine.AllFlags()];
			var isPublic = types.Contains(ListType.Public);
			var isPrivate = types.Contains(ListType.Mine) && ProfileId.HasValue;

			if (!isPublic && !isPrivate)
			{
				bob.AppendLine("\tAND FALSE");
				return;
			}

			if (!isPublic)
			{
				parameters.AddDynamicParams(new { profileId = ProfileId });
				bob.AppendLine("\tAND l.profile_id = @profileId");
				return;
			}

			if (!isPrivate)
			{
				bob.AppendLine("\tAND l.is_public = TRUE");
				return;
			}

			parameters.AddDynamicParams(new { profileId = ProfileId });
			bob.AppendLine("\tAND (l.is_public = TRUE OR l.profile_id = @profileId)");
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
			DROP TABLE IF EXISTS tmp_list_results_{suffix};

			CREATE TEMP TABLE tmp_list_results_{suffix} ON COMMIT DROP AS
			SELECT
				l.id,
				{OrderColumn(Order)} as order_column
			FROM mb_lists l
			JOIN mb_profiles p ON p.id = l.profile_id
			WHERE 
				l.deleted_at IS NULL
				AND p.deleted_at IS NULL
			""");

		HandleTypes(bob, parameters);

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

		bob.AppendLine($"""
			;
			SELECT COUNT(*) FROM tmp_list_results_{suffix};

			CREATE TEMP TABLE tmp_list_results_{suffix}_ordered ON COMMIT DROP AS
			SELECT id, order_column
			FROM tmp_list_results_{suffix}
			ORDER BY order_column {(Asc ? "ASC" : "DESC")}
			LIMIT :limit OFFSET :offset;
			
			DROP TABLE tmp_list_results_{suffix};

			SELECT DISTINCT tg.*
			FROM mb_lists l
			JOIN tmp_list_results_{suffix}_ordered r ON r.id = l.id
			JOIN mb_list_items i ON i.list_id = l.id
			JOIN mb_manga m ON m.id = i.manga_id
			JOIN mb_manga_tags t ON t.manga_id = m.id
			JOIN mb_tags tg ON tg.id = t.tag_id
			WHERE 
				i.deleted_at IS NULL AND
				l.deleted_at IS NULL AND
				t.deleted_at IS NULL AND
				tg.deleted_at IS NULL AND
				m.deleted_at IS NULL;

			SELECT 
				DISTINCT 
				l.id as first_id, 
				t.tag_id as second_id
			FROM mb_lists l
			JOIN tmp_list_results_{suffix}_ordered r ON r.id = l.id
			JOIN mb_list_items i ON i.list_id = l.id
			JOIN mb_manga m ON m.id = i.manga_id
			JOIN mb_manga_tags t ON t.manga_id = m.id
			WHERE 
				i.deleted_at IS NULL AND
				l.deleted_at IS NULL AND
				t.deleted_at IS NULL AND
				m.deleted_at IS NULL;
				
			; WITH manga_covers AS (
				SELECT
					l.id as list_id,
					p.*,
					ROW_NUMBER() OVER (
						PARTITION BY l.id
						ORDER BY i.created_at ASC, p.ordinal DESC
					) as rn
				FROM mb_lists l
				JOIN tmp_list_results_{suffix}_ordered r ON r.id = l.id
				JOIN mb_list_items i ON i.list_id = l.id
				JOIN mb_manga m ON m.id = i.manga_id
				JOIN mb_images p ON p.manga_id = m.id AND p.chapter_id IS NULL
				WHERE
					l.deleted_at IS NULL AND
					i.deleted_at IS NULL AND
					p.deleted_at IS NULL AND
					m.deleted_at IS NULL AND
					p.last_failed_at IS NULL
			)
			SELECT * 
			FROM manga_covers
			WHERE rn = 1;

			SELECT l.*, r.order_column
			FROM mb_lists l
			JOIN tmp_list_results_{suffix}_ordered r ON r.id = l.id
			ORDER BY r.order_column {(Asc ? "ASC" : "DESC")};

			DROP TABLE tmp_list_results_{suffix}_ordered;
			COMMIT;
			""");

		return bob.ToString();
	}
}
