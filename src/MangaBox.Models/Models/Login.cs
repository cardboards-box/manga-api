namespace MangaBox.Models;

/// <summary>
/// Represents a third party connection to a user's profile
/// </summary>
[Table("mb_logins")]
public class Login : DbObject
{
    /// <summary>
    /// The ID of the profile this login is tied to
    /// </summary>
    [Column("profile_id")]
    public required Guid ProfileId { get; set; }

    /// <summary>
    /// The ID of the user's account on the authentication system
    /// </summary>
    [Column("platform_id", Unique = true)]
    public required string PlatformId { get; set; }

    /// <summary>
    /// The username of the user on the platform
    /// </summary>
    [Column("username")]
    public required string Username { get; set; }

    /// <summary>
    /// The user's avatar on the platform
    /// </summary>
    [Column("avatar")]
    public required string Avatar { get; set; }

    /// <summary>
    /// The type of platform (discord, GitHub, twitch, etc)
    /// </summary>
    [Column("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// The ID of the user's account on the platform (discord ID, GitHub ID, etc)
    /// </summary>
    [Column("provider_id")]
    public required string ProviderId { get; set; }

    /// <summary>
    /// The user's email address
    /// </summary>
    [Column("email")]
    public required string Email { get; set; }
}
