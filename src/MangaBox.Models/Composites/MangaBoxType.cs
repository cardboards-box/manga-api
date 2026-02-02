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

	/// <summary>
	/// Fetches all of the related entities of a specific type
	/// </summary>
	/// <typeparam name="TItem">The type to fetch</typeparam>
	/// <returns>The fetched types</returns>
	public IEnumerable<TItem> GetItems<TItem>()
		where TItem : class, IDbModel
	{
		return Related
			.Where(r => r.Data is TItem)
			.Select(r => r.Data as TItem!)!;
	}

	/// <summary>
	/// Fetches the first instance of the given type
	/// </summary>
	/// <typeparam name="TItem">The type to fetch</typeparam>
	/// <returns>The item or default if it was not found</returns>
	public TItem? GetItem<TItem>()
		where TItem : class, IDbModel
	{
		return GetItems<TItem>().FirstOrDefault();
	}

	/// <summary>
	/// Sees if any of the given type exists
	/// </summary>
	/// <typeparam name="TItem">The type to check for</typeparam>
	/// <returns><see langword="true" /> if any exist, otherwise <see langword="false" /></returns>
	public bool Any<TItem>()
		where TItem : class, IDbModel
	{
		return Related.Any(r => r.Data is TItem);
	}
}
