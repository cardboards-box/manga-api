namespace MangaBox.Models.Composites;

/// <summary>
/// Represents the legacy IDs for an entity
/// </summary>
/// <param name="ParentId">The ID of the parent item</param>
/// <param name="ChildIds">The IDs of all of the children</param>
public record class LegacyIds(
	int ParentId,
	Dictionary<string, int>? ChildIds = null);
