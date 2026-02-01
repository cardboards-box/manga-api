namespace MangaBox.Models.Composites;

/// <summary>
/// Represents a relationship entity for a <see cref="MangaBoxType{T}"/>
/// </summary>
[Interface(typeof(IDbModel), nameof(Type), nameof(Data))]
[JsonConverter(typeof(InterfaceParser<MangaBoxRelationship>))]
public class MangaBoxRelationship
{
	/// <summary>
	/// The map of all of the interface options for mapping
	/// </summary>
	[JsonIgnore]
	public static InterfaceMap<MangaBoxRelationship> Map { get; } = new();

	/// <summary>
	/// The name of the type of the relationship
	/// </summary>
	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// The data of the related entity
	/// </summary>
	[JsonPropertyName("data")]
	public IDbModel? Data { get; set; }

	/// <summary>
	/// Generates an instance of the relationship from the given entity
	/// </summary>
	/// <param name="model">The entity to create the relationship from</param>
	/// <returns>The relationship</returns>
	/// <exception cref="Exception">Thrown if the given type isn't a valid map</exception>
	public static MangaBoxRelationship? FromEntity(IDbModel? model)
	{
		if (model is null) return null;

		var type = model.GetType();
		var name = Map.Map.FirstOrDefault(t => type == t.Map)
			?? throw new Exception($"{nameof(MangaBoxRelationship)} >> {type.Name} not mapped");

		return new MangaBoxRelationship
		{
			Type = name.Name,
			Data = model
		};
	}

	/// <summary>
	/// Applies the relationship to the given list of items
	/// </summary>
	/// <param name="items">The given list of items</param>
	/// <param name="entity">The entity to add</param>
	public static void Apply(List<MangaBoxRelationship> items, IDbModel? entity)
	{
		if (entity is null) return;
		var relationship = FromEntity(entity);
		if (relationship is null) return;

		items.Add(relationship);
	}

	/// <summary>
	/// Applies the relationship to the given list of items
	/// </summary>
	/// <param name="items">The given list of items</param>
	/// <param name="entities">All of the entities to add</param>
	public static void Apply(List<MangaBoxRelationship> items, IEnumerable<IDbModel?> entities)
	{
		foreach (var entity in entities)
			Apply(items, entity);
	}
}
