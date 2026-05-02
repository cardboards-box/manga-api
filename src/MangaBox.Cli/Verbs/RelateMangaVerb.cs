namespace MangaBox.Cli.Verbs;

using Models;
using Services;

[Verb("relate-manga", HelpText = "Scans the database and relates manga together")]
internal class RelateMangaOptions
{ 

}

internal class RelateMangaVerb(
	ISqlService _sql,
	IRelatingService _relate,
	ILogger<RelateMangaVerb> logger) : BooleanVerb<RelateMangaOptions>(logger)
{
	public async Task<RelatedManga[]> GetRelated()
	{
		const string QUERY = """
			WITH all_titles AS (
			    SELECT
			        m.id,
			        LOWER(m.title) as title
			    FROM mb_manga m
			    WHERE m.deleted_at IS NULL

			    UNION

			    SELECT
			        m.id as id,
			        LOWER(UNNEST(m.alt_titles)) as title
			    FROM mb_manga m
			    WHERE m.deleted_at IS NULL
			), related_titles AS (
			SELECT
			    DISTINCT
			    a.id as first_manga_id,
			    b.id as second_manga_id,
			    a.title
			FROM all_titles a
			JOIN all_titles b ON
			    a.id <> b.id AND
			    a.title = b.title
			), manga_details AS (
			    SELECT
			        first_manga_id as id,
			        second_manga_id as other
			    FROM related_titles

			    UNION

			    SELECT
			        second_manga_id as id,
			        first_manga_id as other
			    FROM related_titles
			)
			SELECT
			    m.*,
			    '' as split,
			    e.*,
			    '' as split,
			    s.*,
			    '' as split,
			    d.other
			FROM manga_details d
			JOIN mb_manga m ON d.id = m.id
			JOIN mb_manga_ext e ON e.manga_id = m.id
			JOIN mb_sources s ON s.id = m.source_id
			WHERE
			    m.deleted_at IS NULL AND
			    e.deleted_at IS NULL AND
			    s.deleted_at IS NULL
			ORDER BY m.id, d.other;
			""";

		using var con = await _sql.CreateConnection();
		return [..await con.QueryAsync<MbManga, MbMangaExt, MbSource, RelatedIds, RelatedManga>(
			QUERY,
			(manga, ext, source, ids) => new(manga, ext, source, ids),
			splitOn: "split"
		)];
	}

	public async Task<RelatedManga[][]> DetermineRelated()
	{
		var all = await GetRelated();
		if (all.Length == 0) return [];

		//The grouped manga with the first found ID as the key
		var groupings = new Dictionary<Guid, Dictionary<Guid, RelatedManga>>();
		//A mapping of the manga's mapped already to the first found ID, to prevent duplicates
		var mapped = new Dictionary<Guid, Guid>();

		foreach(var manga in all)
		{
			//If the manga is already grouped, add it to the existing group and map it to the existing ID
			if (groupings.TryGetValue(manga.Id, out var value))
			{
				value[manga.Ids.Other] = manga;
				mapped[manga.Id] = manga.Id;
				mapped[manga.Ids.Other] = manga.Id;
				continue;
			}

			//Try to find any mapped manga, and add it to the existing group
			Guid? mappedId = mapped.TryGetValue(manga.Id, out var id)
				? id : mapped.TryGetValue(manga.Ids.Other, out id)
				? id : null;
			if (mappedId.HasValue)
			{
				groupings[mappedId.Value][manga.Id] = manga;
				mapped[manga.Id] = mappedId.Value;
				continue;
			}

			//Create a new group for the manga
			groupings[manga.Id] = new Dictionary<Guid, RelatedManga>
			{
				[manga.Ids.Other] = manga
			};
			mapped[manga.Id] = manga.Id;
			mapped[manga.Ids.Other] = manga.Id;
		}

		return [..groupings.Values.Select(g => g.Values.ToArray()).Where(t => t.Length > 1)];
	}

	public override async Task<bool> Execute(RelateMangaOptions options, CancellationToken token)
	{
		var related = await DetermineRelated();
		if (related.Length == 0)
		{
			_logger.LogInformation("No related manga found");
			return false;
		}

		foreach(var group in related)
		{
			var manga = group.Select(t => t.Manga).ToArray();
			await _relate.Relate(manga);
		}

		var total = related.Sum(g => g.Length);
		_logger.LogInformation("Related {Count} groups of manga, {Total} manga in total", related.Length, total);
		return true;
	}
}

internal class RelatedIds
{
	public Guid Other { get; set; }
}

internal record class RelatedManga(
	MbManga Manga,
	MbMangaExt Ext,
	MbSource Source,
	RelatedIds Ids)
{
	public Guid Id => Manga.Id;
}