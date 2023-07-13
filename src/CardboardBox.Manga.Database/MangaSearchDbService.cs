namespace CardboardBox.Manga.Database;

using Models.Requests.Filtering;

public interface IMangaSearchDbService
{
    Task<Filter[]> Filters();

    Task<PaginatedResult<MangaExtended>> Search(MangaFilter filter, string? platformId);

    Task<GraphOut[]> Graphic(string? platformId, TouchedState state = TouchedState.Completed);
}

public class MangaSearchDbService : IMangaSearchDbService
{
    private readonly ISqlService _sql;

    public MangaSearchDbService(ISqlService sql)
    {
        _sql = sql;
    }

    public async Task<Filter[]> Filters()
    {
        const string QUERY = @"WITH allTags as (
    SELECT
        DISTINCT
        'tag' as key,
        unnest(tags) as value,
        nsfw as nsfw
    FROM manga
), sfwTags as (
    SELECT
        DISTINCT
        key,
        LOWER(value) as value
    FROM allTags
	WHERE nsfw = False
), nsfwTags as (
    SELECT
        DISTINCT
        'nsfw-tag' as key,
        LOWER(value) as value
    FROM allTags
    WHERE LOWER(value) NOT IN (
        SELECT
            LOWER(value)
        FROM sfwTags
    )
), attributes AS (
    SELECT
        DISTINCT
        lower((attr).name) as key,
        (attr).value as value
    FROM (
        SELECT
            DISTINCT
            unnest(attributes) as attr
        FROM manga
    ) z
    WHERE (attr).name NOT IN ('Author', 'Artist')
), sources AS (
    SELECT
        DISTINCT
        'source' as key,
        provider as value
    FROM manga
)
SELECT
    *
FROM (
    SELECT * FROM sfwTags
    UNION ALL
    SELECT * FROM nsfwTags
    UNION ALL
    SELECT * FROM attributes
    UNION ALL
    SELECT * FROM sources
) x
ORDER BY key, value";
        var filters = await _sql.Get<DbFilter>(QUERY);
        return filters
            .GroupBy(t => t.Key, t => t.Value)
            .Select(t => new Filter(t.Key, t.ToArray()))
            .Append(new Filter("sorts", SortFields().Select(t => t.Name).ToArray()))
            .ToArray();
    }

    public async Task<PaginatedResult<MangaExtended>> Search(MangaFilter filter, string? platformId)
    {
        const string FILTER_QUERY = @"WITH touched_manga AS (
	SELECT
		*
	FROM get_manga_filtered(:platformId,:state, ARRAY(
		SELECT
			m.id
		FROM manga m
		LEFT JOIN manga_attributes a ON a.id = m.id
		WHERE {0}
	))
)";

        const string SEARCH_QUERY = FILTER_QUERY + @"
SELECT
    m.*,
    '' as split,
    mp.*,
    '' as split,
    mc.*,
    '' as split,
    t.*
FROM touched_manga t
JOIN manga m ON m.id = t.manga_id
JOIN manga_chapter mc ON mc.id = t.manga_chapter_id
LEFT JOIN manga_progress mp ON mp.id = t.progress_id
ORDER BY {2} {1}
LIMIT :size OFFSET :offset";

        const string COUNT_QUERY = FILTER_QUERY + @"
SELECT COUNT(*) FROM touched_manga";

        var sortField = SortFields().FirstOrDefault(t => t.Id == (filter.Sort ?? 0))?.SqlName ?? "m.title";

        var parts = new List<string>();
        var pars = new DynamicParameters();
        pars.Add("offset", (filter.Page - 1) * filter.Size);
        pars.Add("size", filter.Size);
        pars.Add("platformId", platformId);
        pars.Add("state", (int)filter.State);

        if (filter.Attributes != null && filter.Attributes.Length > 0)
        {
            for (var i = 0; i < filter.Attributes.Length; i++)
            {
                var attr = filter.Attributes[i];
                var name = $"attr{i}";
                var type = attr.Type switch
                {
                    AttributeType.ContentRating => "content rating",
                    AttributeType.Status => "status",
                    AttributeType.OriginalLanguage => "original language",
                    _ => null
                };

                if (type == null || attr.Values == null || attr.Values.Length == 0) continue;

                pars.Add(name + "val", attr.Values);
                pars.Add(name + "type", type);

                if (attr.Include)
                {
                    parts.Add($"(LOWER(a.name) = :{name}type AND a.value = ANY( :{name}val ))");
                    continue;
                }

                parts.Add($"(LOWER(a.name) = :{name}type AND NOT (a.value = ANY( :{name}val )))");
            }
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            parts.Add("m.fts @@ phraseto_tsquery('english', :search)");
            pars.Add("search", filter.Search);
        }

        if (filter.Sources != null && filter.Sources.Length > 0)
        {
            parts.Add("m.provider = ANY( :source )");
            pars.Add("source", filter.Sources);
        }

        if (filter.Include != null && filter.Include.Length > 0)
        {
            parts.Add("(LOWER(m.tags::text)::text[]) @> :include");
            pars.Add("include", filter.Include);
        }

        if (filter.Exclude != null && filter.Exclude.Length > 0)
        {
            parts.Add("NOT ((LOWER(m.tags::text)::text[]) && :exclude )");
            pars.Add("exclude", filter.Exclude);
        }

        if (filter.Nsfw != NsfwCheck.DontCare)
        {
            parts.Add("m.nsfw = :nsfw");
            pars.Add("nsfw", filter.Nsfw == NsfwCheck.Nsfw);
        }

        parts.Add("m.deleted_at IS NULL");
        var where = string.Join(" AND ", parts);
        var sort = filter.Ascending ? "ASC" : "DESC";

        var search = string.Format(SEARCH_QUERY, where, sort, sortField);
        var count = string.Format(COUNT_QUERY, where);

        var offset = (filter.Page - 1) * filter.Size;
        using var con = await _sql.CreateConnection();

        var results = await con.QueryAsync<DbManga, DbMangaProgress, DbMangaChapter, MangaStats, MangaExtended>(
            search,
            (m, p, c, s) => new MangaExtended(m, p, c, s),
            splitOn: "split",
            param: pars);

        var total = await con.ExecuteScalarAsync<int>(count, pars);
        var pages = (int)Math.Ceiling((double)total / filter.Size);
        return new PaginatedResult<MangaExtended>(pages, total, results.ToArray());
    }

    public static MangaSortField[] SortFields()
    {
        return new[]
        {
            new MangaSortField("Title", 0, "m.title"),
            new("Provider", 1, "m.provider"),
            new("Latest Chapter", 2, "t.latest_chapter"),
            new("Description", 3, "m.description"),
            new("Updated", 4, "m.updated_at"),
            new("Created", 5, "m.created_at")
        };
    }

    public Task<GraphOut[]> Graphic(string? platformId, TouchedState state = TouchedState.Completed)
    {
        const string QUERY = @"CREATE TEMP TABLE touched_manga AS
SELECT DISTINCT manga_id
FROM get_manga(:platformId, :state);

SELECT
    'tag' as type,
    x.tag as key,
    COUNT(*) as count
FROM (
    SELECT unnest(m.tags) as tag
    FROM touched_manga t
    JOIN manga m ON m.id = t.manga_id
) x
JOIN (
    SELECT DISTINCT unnest(tags) as tag
    FROM manga
    WHERE nsfw = false
) n ON n.tag = x.tag
GROUP BY x.tag
ORDER BY COUNT(*) DESC;";
        return _sql.Get<GraphOut>(QUERY, new { platformId, state });
    }
}
