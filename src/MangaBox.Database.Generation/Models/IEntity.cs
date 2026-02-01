namespace MangaBox.Database.Generation.Models;

/// <summary>
/// A type of database entity that can be generated
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The name of the file to generate
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The implementation of the entity
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// All of the types this entity requires to be generated
    /// </summary>
    HashSet<Type> Requires { get; }
}
