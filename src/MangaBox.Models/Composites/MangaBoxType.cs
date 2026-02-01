namespace MangaBox.Models.Composites;

/// <summary>
/// Represents a type that has relationships with other types
/// </summary>
public class MangaBoxType<T>
{
	/// <summary>
	/// The data of the type
	/// </summary>
	[JsonPropertyName("entity")]
	public T Entity { get; set; }

	/// <summary>
	/// All of the related entities
	/// </summary>
	[JsonPropertyName("related")]
	public MangaBoxRelationship[] Related { get; set; }

	/// <summary>
	/// Represents a type that has relationships with other types
	/// </summary>
	[JsonConstructor]
	internal MangaBoxType()
	{
		Entity = default!;
		Related = [];
	}

	/// <summary>
	/// Represents a type that has relationships with other types
	/// </summary>
	/// <param name="entity">The data of the type</param>
	/// <param name="related">All of the related entities</param>
	public MangaBoxType(T entity, MangaBoxRelationship[] related)
	{
		Entity = entity;
		Related = related;
	}
}
