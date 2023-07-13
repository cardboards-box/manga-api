namespace CardboardBox.Manga.Database.Base;

public interface IOrmMap<T> where T : DbObject
{
    Task<T?> Fetch(long id);
    Task<T[]> Get();
    Task<T[]> GetWithDeleted();
    Task<int> Insert(T item);
    Task<int> Update(T item);
    Task<int> Delete(long id);
    Task<PaginatedResult<T>> Paginate(int page = 1, int size = 100);
    Task<long> Upsert(T item);
}

public abstract class OrmMap<T> : IOrmMap<T> where T : DbObject
{
    #region Query Cache
    private static string? _fetchQuery;
    private static string? _insertQuery;
    private static string? _updateQuery;
    private static string? _deleteQuery;
    private static string? _paginateQuery;
    private static string? _getQuery;
    private static string? _getWithDeletedQuery;
    private static string? _upsertFakeSelect;
    private static string? _upsertFakeInsert;
    private static string? _upsertFakeUpdate;
    #endregion

    public readonly IQueryService _query;
    public readonly ISqlService _sql;
    public readonly IFakeUpsertQueryService _fakeUpserts;

    public OrmMap(
        IQueryService query,
        ISqlService sql,
        IFakeUpsertQueryService fakeUpserts)
    {
        _query = query;
        _sql = sql;
        _fakeUpserts = fakeUpserts;
    }

    #region Implementations
    public virtual Task<T?> Fetch(long id)
    {
        _fetchQuery ??= _query.Fetch<T>();
        return _sql.Fetch<T>(_fetchQuery, new { Id = id });
    }

    public virtual Task<T[]> Get()
    {
        _getQuery ??= _query.Select<T>(a => a.Null(t => t.DeletedAt));
        return _sql.Get<T>(_getQuery);
    }

    public virtual Task<T[]> GetWithDeleted()
    {
        _getWithDeletedQuery ??= _query.Select<T>();
        return _sql.Get<T>(_getWithDeletedQuery);
    }

    public virtual Task<int> Insert(T item)
    {
        _insertQuery ??= _query.Insert<T>();
        return _sql.Execute(_insertQuery, item);
    }

    public virtual Task<int> Update(T item)
    {
        _updateQuery ??= _query.Update<T>();
        return _sql.Execute(_updateQuery, item);
    }

    public virtual Task<int> Delete(long id)
    {
        _deleteQuery ??= _query.Delete<T>();
        return _sql.Execute(_deleteQuery, new { Id = id });
    }

    public virtual Task<PaginatedResult<T>> Paginate(int page = 1, int size = 100)
    {
        _paginateQuery ??= _query.Paginate<T, DateTime>(a => a.CreatedAt, true, t => t.Null(a => a.DeletedAt));
        return _sql.Paginate<T>(_paginateQuery, null, page, size);
    }

    public virtual async Task<long> Upsert(T item)
    {
        //Why? See the note in the FakeUpsertQueryService.cs file.
        if (_upsertFakeInsert == null || _upsertFakeSelect == null || _upsertFakeUpdate == null)
        {
            var (insert, update, select) = _fakeUpserts.FakeUpsert<T>();
            _upsertFakeInsert = insert + " RETURNING id";
            _upsertFakeUpdate = update;
            _upsertFakeSelect = select;
        }

        var exists = await _sql.Fetch<T>(_upsertFakeSelect, item);
        if (exists == null)
            return await _sql.ExecuteScalar<long>(_upsertFakeInsert, item);
        await _sql.Execute(_upsertFakeUpdate, item);
        return exists.Id;
    }
    #endregion
}
