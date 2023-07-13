namespace CardboardBox.Manga.Models;

/// <summary>
/// Represents a composite object that is the result of a multi-return result query.
/// These could be tuples, but are objects for the sake of clarity.
/// 
/// These are not mapped to database objects, so they don't need to be registered as a model
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CompositeTupleAttribute : Attribute { }
