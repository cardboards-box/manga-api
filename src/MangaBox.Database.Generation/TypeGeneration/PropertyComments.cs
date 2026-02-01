namespace MangaBox.Database.Generation.TypeGeneration;

/// <summary>
/// The comments for a type
/// </summary>
/// <param name="Parent">The type that owns this property</param>
/// <param name="Property">The property this comment belongs to</param>
/// <param name="Summary">The summary comments for the type</param>
/// <param name="Remarks">The remarks comments for the type</param>
/// <param name="Example">The example comments for the type</param>
/// <param name="TypeComments">The comments associated with the property type</param>
public record class PropertyComments(
    Type Parent,
    PropertyInfo Property,
    TypeComments TypeComments,
    string? Summary,
    string? Remarks,
    string? Example);