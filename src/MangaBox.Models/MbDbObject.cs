namespace MangaBox.Models;

/// <summary>
/// The base class for most MangaBox database tables
/// </summary>
/// <remarks>You should only decorate entities that should be audited with this class</remarks>
public abstract class MbDbObject : DbObject, IDbTable, IValidator
{

}

/// <summary>
/// The base class for any MangaBox database tables that have a legacy counterpart
/// </summary>
public abstract class MbDbObjectLegacy : MbDbObject
{
	/// <summary>
	/// The legacy ID of the entity
	/// </summary>
	[Column("legacy_id", ExcludeUpdates = true)]
	[JsonPropertyName("legacyId")]
	public int? LegacyId { get; set; }
}