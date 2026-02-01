namespace MangaBox.Core;

/// <summary>
/// Indicates that a class should have one or more full-text-search indexes created for it
/// </summary>
/// <param name="_fields">All of the columns that should be included in the full-text-search index</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class SearchableAttribute(params string[] _fields) : Attribute
{
	/// <summary>
	/// Indicates the name of the column to apply this column to (default: fts)
	/// </summary>
	public string ColumnName { get; set; } = "fts";

	/// <summary>
	/// The language to use for the ts-vector column (default: english)
	/// </summary>
	public string Language { get; set; } = "english";

	/// <summary>
	/// All of the columns that should be included in the full-text-search index
	/// </summary>
	public string[] Fields { get; set; } = _fields;
}
