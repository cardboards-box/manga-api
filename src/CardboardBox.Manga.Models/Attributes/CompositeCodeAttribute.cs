namespace CardboardBox.Manga.Models;

/// <summary>
/// These are composite objects that represent the result of a query that has been manipulated in code.
/// These do NOT map directly to the result of a query.
/// 
/// These are not mapped to database objects, so they don't need to be registered as a model
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CompositeCodeAttribute : Attribute { }
