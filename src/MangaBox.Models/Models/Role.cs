namespace MangaBox.Models;

/// <summary>
/// Represents a role in the system
/// </summary>
[Table("mb_roles")]
public class Role : Orderable
{
    /// <summary>
    /// User has global admin permissions across the application
    /// </summary>
    public const string ADMIN = "Admin";

    /// <summary>
    /// User has some special permissions, but not as much as an admin
    /// </summary>
    public const string MODERATOR = "Moderator";

    /// <summary>
    /// User has no special permissions
    /// </summary>
    public const string USER = "User";

    /// <summary>
    /// User is a queue agent account
    /// </summary>
    public const string AGENT = "Agent";

    /// <summary>
    /// The unique name of the role
    /// </summary>
    [Column("name", Unique = true)]
    public required string Name { get; set; }

    /// <summary>
    /// A description of the role
    /// </summary>
    [Column("description")]
    public required string Description { get; set; }

    /// <summary>
    /// The google material icon or discord emote for the role
    /// </summary>
    [Column("icon")]
    public required string Icon { get; set; }
}
