namespace MangaBox.Database.Generation.TypeGeneration;

/// <summary>
/// The comments for a type
/// </summary>
/// <param name="Type">The type the comments belong to</param>
/// <param name="Summary">The summary comments for the type</param>
/// <param name="Remarks">The remarks comments for the type</param>
/// <param name="Example">The example comments for the type</param>
public record class TypeComments(
    Type Type,
    string? Summary,
    string? Remarks,
    string? Example)
{
    /// <summary>
    /// The comments for the properties
    /// </summary>
    public PropertyComments[] Properties { get; set; } = [];
}
