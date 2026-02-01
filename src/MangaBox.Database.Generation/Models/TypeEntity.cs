namespace MangaBox.Database.Generation.Models;

internal record class TypeEntity(
    Type Type,
    IDbType DbType,
    TypeAttribute? Attribute,
    CreateArrayJoinAttribute? ArrayJoin,
    string FileName,
    string? FunctionPath,
    HashSet<Type> Requires,
    params TableColumn[] Columns) : IEntity
{
    /// <summary>
    /// The name of the type
    /// </summary>
    public string Name => Attribute?.Name?.ForceNull() ?? Type.Name;

    /// <summary>
    /// All of the enum types in the table
    /// </summary>
    public IEnumerable<Type> Enums => Columns.Where(t => t.IsEnum).Select(t => t.Type);
}

