namespace MangaBox.Database.Services;

using Models;

public interface IPersonDbService : IOrmMap<Person>
{
    Task<Person[]> Get(Guid[] ids);

    Task<PersonMap[]> BySeries(Guid id);
}

internal class PersonDbService(IOrmService orm) : Orm<Person>(orm), IPersonDbService
{
    public Task<Person[]> Get(Guid[] ids)
    {
        const string QUERY = "SELECT * FROM mb_people WHERE id = ANY(@Ids)";
        return Get(QUERY, new { Ids = ids });
    }

    public async Task<PersonMap[]> BySeries(Guid id)
    {
        const string QUERY = @"
SELECT 
    ppl.*,
    '' as split,
    sp.*
FROM mb_series_people sp
JOIN mb_people ppl ON sp.person_id = ppl.id
WHERE sp.series_id = @Id
";
        return (await _sql.QueryTupleAsync<Person, SeriesPeople>(QUERY, new { Id = id }))
            .Select(t => new PersonMap(t.item1, t.item2))
            .ToArray(); ;
    }
}
