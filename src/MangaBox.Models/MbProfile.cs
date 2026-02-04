namespace MangaBox.Models;

using static Constants;

/// <summary>
/// Represents a person who has logged into MangaBox
/// </summary>
[Table("mb_profiles")]
public class MbProfile : MbDbObjectLegacy
{
	/// <summary>
	/// The user's name
	/// </summary>
	[Column("username")]
	[JsonPropertyName("username")]
	[Required, MinLength(MIN_NAME_LENGTH), MaxLength(MAX_NAME_LENGTH)]
	public string Username { get; set; } = string.Empty;

	/// <summary>
	/// The user's avatar
	/// </summary>
	[Column("avatar")]
	[JsonPropertyName("avatar")]
	public string? Avatar { get; set; }

	/// <summary>
	/// The ID of the user within CBA auth
	/// </summary>
	[Column("platform_id", Unique = true)]
	[JsonPropertyName("platformId")]
	[Required]
	public string PlatformId { get; set; } = string.Empty;

	/// <summary>
	/// The name of the OAuth platform the user signed in with
	/// </summary>
	[Column("provider")]
	[JsonPropertyName("provider")]
	[Required]
	public string Provider { get; set; } = string.Empty;

	/// <summary>
	/// The unique ID of the user on the OAuth platform the user signed in with
	/// </summary>
	[Column("provider_id")]
	[JsonPropertyName("providerId")]
	[Required]
	public string ProviderId { get; set; } = string.Empty;

	/// <summary>
	/// The user's email
	/// </summary>
	[Column("email")]
	[JsonPropertyName("email")]
	[Required]
	public string Email { get; set; } = string.Empty;

	/// <summary>
	/// The optional JSON blob of settings for the user
	/// </summary>
	[Column("settings_blob", ExcludeUpdates = true)]
	[JsonPropertyName("settingsBlob")]
	public string? SettingsBlob { get; set; }

	/// <summary>
	/// Whether or not the user is an administrator
	/// </summary>
	[Column("admin", ExcludeUpdates = true)]
	[JsonPropertyName("admin")]
	public bool Admin { get; set; } = false;

	/// <summary>
	/// Whehter or not the user can read manga on the platform
	/// </summary>
	[Column("can_read", ExcludeUpdates = true)]
	[JsonPropertyName("canRead")]
	public bool CanRead { get; set; } = false;
}
