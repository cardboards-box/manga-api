namespace CardboardBox.Manga.Models;

/// <summary>
/// Represents a composite object that is the result of a query.
/// 
/// These are mapped to database objects, so they need to be registered as a model
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CompositeAttribute : Attribute { }
