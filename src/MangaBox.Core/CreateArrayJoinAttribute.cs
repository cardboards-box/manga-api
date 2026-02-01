namespace MangaBox.Core;

/// <summary>
/// Indicates that an array join function should be created for this table
/// </summary>
/// <param name="prefix">The prefix to use for the generated method</param>
/// <param name="property">The name of the property to join on (the property must be an array type)</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CreateArrayJoinAttribute(string prefix, string property) : Attribute
{
	/// <summary>
	/// The prefix to use for the generated method
	/// </summary>
	public string Prefix { get; set; } = prefix;

	/// <summary>
	/// The name of the property to join on (the property must be an array type)
	/// </summary>
	public string Property { get; set; } = property;

	/// <summary>
	/// The name of the generated method (the full name will be {Prefix}{Name})
	/// </summary>
	public string Name { get; set; } = "_array_join";

	/// <summary>
	/// Gets the full name, including any prefix, for the current instance.
	/// </summary>
	public string FullName => $"{Prefix}{Name}".Trim();
}