namespace MangaBox.Database.Generation.Models;

/// <summary>
/// Represents a one to many relationship between two entities
/// </summary>
/// <param name="One">The parent type of the relationship</param>
/// <param name="Many">The child type of the relationship</param>
/// <param name="Attribute">The attribute that triggered the relationship</param>
internal record class OneToManyRelationship(
    Type One,
    Type Many,
    FkAttribute Attribute) : IEntityRelationship;
