/*
    This is purely here because of how postgres handles upserts using the `CONFLICTING` keyword.
    Unfortunately, it auto-incremenets the primary-keys sequence even if it was an update.
    This results in massive disconnects between sequential IDs and just looks bad.
    To fix this, instead of using the `CONFLICTING` keyword, we use a fake upsert.
*/

namespace CardboardBox.Manga.Database.Base;

public interface IFakeUpsertQueryService
{
    (string insert, string update, string select) FakeUpsert<T>(QueryConfig? config = null);
}

public class FakeUpsertQueryService : IFakeUpsertQueryService
{
    private readonly IQueryService _query;
    private readonly IQueryGenerationService _gen;
    private readonly IQueryConfigProvider _config;

    public FakeUpsertQueryService(
        IQueryService query,
        IQueryGenerationService gen,
        IQueryConfigProvider config)
    {
        _query = query;
        _gen = gen;
        _config = config;
    }

    public (string insert, string update, string select) FakeUpsert<T>(QueryConfig? config = null)
    {
        config ??= _config.GetQueryConfig();
        if (_query is not QueryService query)
            throw new ArgumentException($"Expected {nameof(_query)} to implement {nameof(QueryService)}.");

        var map = query.Type<T>();
        var uq = map.Properties
            .Values
            .Where(t => t.Column?.Unique ?? false)
            .ToArray();

        if (!uq.Any()) throw new ArgumentException("No primary key found for type: " + map.Name);

        var select = _gen.Select(map.Name, config, query.From(uq, false));

        return (_query.Insert<T>(config), _query.Update<T>(config: config), select);
    }
}
