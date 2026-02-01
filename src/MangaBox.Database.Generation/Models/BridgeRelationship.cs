namespace MangaBox.Database.Generation.Models;

/// <summary>
/// Represents a bridge relationship between two entities
/// </summary>
/// <param name="Attribute">The attribute that triggered the relationship</param>
/// <param name="Parent">The entity with the lest expected entries for the relationship</param>
/// <param name="Child">The entity with the most expected entries for the relationship</param>
/// <param name="Bridge">The entity that serves as the bridge between the other tables</param>
internal record class BridgeRelationship(
    BridgeTableAttribute Attribute,
    BridgeEntity Parent,
    BridgeEntity Child,
    TableEntity Bridge) : IEntityRelationship;

/// <summary>
/// Represents the 
/// </summary>
/// <param name="Table">The table the entity is from</param>
/// <param name="Column">The column on the table the entity is from</param>
/// <param name="BridgeColumn">The column from the bridge table</param>
internal record class BridgeEntity(
    TableEntity Table,
    TableColumn Column,
    TableColumn BridgeColumn);