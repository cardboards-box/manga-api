namespace MangaBox.Models;

/// <summary>
/// Represents a database object that has audit fields
/// </summary>
public abstract class Auditable : DbObject
{
    /// <summary>
    /// The ID of the <see cref="Profile"/> who created this object
    /// </summary>
    [JsonPropertyName("createdBy")]
    [Column("created_by", ExcludeUpdates = true)]
    public virtual Guid CreatedBy { get; set; }

    /// <summary>
    /// The ID of the <see cref="Profile"/> who last updated this object
    /// </summary>
    [JsonPropertyName("updatedBy")]
    [Column("updated_by")]
    public virtual Guid UpdatedBy { get; set; }

    /// <summary>
    /// The ID of the <see cref="Profile"/> who deleted this object
    /// </summary>
    [JsonPropertyName("deletedBy")]
    [Column("deleted_by")]
    public virtual Guid? DeletedBy { get; set; }

    /// <summary>
    /// Sets the audit fields for this object
    /// </summary>
    /// <param name="userId">The user who is creating or updating the object</param>
    public void Audit(Guid userId)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }
}
