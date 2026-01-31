namespace MangaBox.Models;

/// <summary>
/// Represents an orderable object in the database
/// </summary>
public abstract class Orderable : Auditable
{
    /// <summary>
    /// The order this item appears
    /// </summary>
    [Column("ordinal")]
    public virtual required double Ordinal { get; set; }
}
