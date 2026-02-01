namespace MangaBox.Database.Generation.Models;

/// <summary>
/// Represents all of the entities in the system
/// </summary>
/// <param name="Tables">All of the tables in the system</param>
/// <param name="Types">All of the table types in the system</param>
/// <param name="Relationships">All of the relationships between the tables</param>
internal record class Entities(
    TableEntity[] Tables,
    TypeEntity[] Types,
    IEntityRelationship[] Relationships)
{
    /// <summary>
    /// All of the entities in the system
    /// </summary>
    public IEnumerable<IEntity> All => Types.Cast<IEntity>().Concat(Tables);

    /// <summary>
    /// All of the enum types used in the system
    /// </summary>
    public IEnumerable<Type> Enums => Tables.SelectMany(t => t.Enums).Concat(Types.SelectMany(t => t.Enums)).Distinct();
}
