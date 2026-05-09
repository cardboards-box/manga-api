namespace MangaBox.Models;

/// <summary>
/// All of the notification devices registered to a user
/// </summary>
[Table("mb_notification_devices")]
public class MbNotificationDevice : MbDbObject
{
	/// <summary>
	/// The ID of the profile that owns this notification device
	/// </summary>
	[Column("profile_id", Unique = true), Fk<MbProfile>]
	[JsonPropertyName("profileId")]
	public Guid ProfileId { get; set; }

	/// <summary>
	/// The display name for the device
	/// </summary>
	[Column("name")]
	[JsonPropertyName("name"), Required, MinLength(1)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The FCM token for this notification device
	/// </summary>
	[Column("fcm_token", Unique = true)]
	[JsonPropertyName("fcmToken"), Required, MinLength(1)]
	public string FcmToken { get; set; } = string.Empty;

	/// <summary>
	/// Whether or not the token is active
	/// </summary>
	[Column("active")]
	[JsonPropertyName("active")]
	public bool Active { get; set; }
}
