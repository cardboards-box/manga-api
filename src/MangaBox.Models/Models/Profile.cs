namespace MangaBox.Models;

/// <summary>
/// A user's profile
/// </summary>
[Table("mb_profiles")]
public class Profile : PublicProfile
{
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
}

public class PublicProfile : DbObject
{
    /// <summary>
    /// The IDs of the roles this profile has
    /// </summary>
    [Column("role_ids")]
    public required Guid[] RoleIds { get; set; }

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

    public static PublicProfile From(Profile profile) => new()
    {
        Id = profile.Id,
        RoleIds = profile.RoleIds,
        Nickname = profile.Nickname,
        Avatar = profile.Avatar,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
        DeletedAt = profile.DeletedAt
    };

    public static PublicProfile[] From(Profile[] profiles) => profiles.Select(From).ToArray();

    public static PublicProfile[] From(IEnumerable<Profile> profiles) => profiles.Select(From).ToArray();

    public static PublicProfile[] From(List<Profile> profiles) => profiles.Select(From).ToArray();

    public static PaginatedResult<PublicProfile> From(PaginatedResult<Profile> profiles) => new()
    {
        Results = From(profiles.Results),
        Count = profiles.Count,
        Pages = profiles.Pages,
    };
}