namespace MangaBox.Models;

/// <summary>
/// A user's profile
/// </summary>
[Table("mb_profiles")]
public class Profile : DbObject
{
    /// <summary>
    /// The IDs of the roles this profile has
    /// </summary>
    [Column("role_ids")]
    public required Guid[] RoleIds { get; set; }

    /// <summary>
    /// The user's settings blob for UI based settings
    /// </summary>
    [Column("settings_blob")]
    public required string? SettingsBlob { get; set; }

    /// <summary>
    /// The ID of the user's primary <see cref="Login"/>
    /// </summary>
    /// <remarks>This is used to get the user's username</remarks>
    [Column("primary_user")]
    public required Guid? PrimaryUser { get; set; }

    /// <summary>
    /// The user's display nickname
    /// </summary>
    [Column("nickname")]
    public required string Nickname { get; set; }

    /// <summary>
    /// The user's avatar URL
    /// </summary>
    [Column("avatar")]
    public required string? Avatar { get; set; }
}
