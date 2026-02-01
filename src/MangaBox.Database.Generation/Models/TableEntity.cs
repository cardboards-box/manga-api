namespace MangaBox.Database.Generation.Models;

/// <summary>
/// Represents a table entity in the database
/// </summary>
/// <param name="Type">The implementation type</param>
/// <param name="DbType">The table type</param>
/// <param name="Table">The attribute that marks the entity as a table</param>
/// <param name="Audit">The attribute that marks the entity as an audit table entity</param>
/// <param name="Searchables">The attributes that mark the entity as having full-text-search columns</param>
/// <param name="Bridges">The attribute that marks the entity as a bridge table</param>
/// <param name="InterfaceOption">The optional instance of the <see cref="InterfaceOptionAttribute"/></param>
/// <param name="FileName">The name of the file</param>
/// <param name="Requires">The required types</param>
/// <param name="Columns">All of the columns of the table</param>
internal record class TableEntity(
    Type Type,
    IDbTable DbType,
    TableAttribute? Table,
    AuditAttribute? Audit,
    SearchableAttribute[] Searchables,
    BridgeTableAttribute[] Bridges,
    InterfaceOptionAttribute? InterfaceOption,
    string FileName,
    HashSet<Type> Requires,
    params TableColumn[] Columns) : IEntity
{
    /// <summary>
    /// The name of the table
    /// </summary>
    public string Name => Table?.Name?.ForceNull() ?? Type.Name;

    /// <summary>
    /// Any prefixes the table name has
    /// </summary>
    public string[] Prefixes => Table?.Prefixes ?? [];

    /// <summary>
    /// Whether or not the entity is cached in the ORM layer
    /// </summary>
    public bool Cache => DbType is IDbCacheTable;

    /// <summary>
    /// All of the foreign keys in the table
    /// </summary>
    public IEnumerable<FkAttribute> ForeignKeys => Columns
        .Where(t => t.IsFk)
        .Select(t => t.ForeignKey!)
        .Distinct();

    /// <summary>
    /// All of the unique columns in the table
    /// </summary>
    public IEnumerable<TableColumn> UniqueCols => Columns.Where(t => t.IsUnique);

    /// <summary>
    /// All of the enum types in the table
    /// </summary>
    public IEnumerable<Type> Enums => Columns.Where(t => t.IsEnum).Select(t => t.Type);

    /// <summary>
    /// The full nam eof the table
    /// </summary>
    /// <returns></returns>
    public string GetFullName()
    {
        return string.Join(".", Prefixes.Append(Name).Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}