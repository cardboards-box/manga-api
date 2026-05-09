namespace MangaBox.Utilities.FCM;

/// <summary>
/// The notification to send
/// </summary>
public class Notification
{
	/// <summary>
	/// The notification title to send
	/// </summary>
	public string? Title { get; set; } = null;

	/// <summary>
	/// The notification body to send
	/// </summary>
	public string? Body { get; set; } = null;

	/// <summary>
	/// The image URL to display with the notification
	/// </summary>
	public string? ImageUrl { get; set; } = null;

	/// <summary>
	/// The notification data to send
	/// </summary>
	public Dictionary<string, string?>? Data { get; set; } = null;

	/// <summary>
	/// The localization settings for the notification title
	/// </summary>
	/// <remarks>
	/// Requires additional setup client-side. See <see href="https://firebase.google.com/docs/cloud-messaging/customize-messages/localize-messages"/>
	/// </remarks>
	public Localization? TitleLocalization { get; set; } = null;

	/// <summary>
	/// The localization settings for the notification body
	/// </summary>
	/// <remarks>
	/// Requires additional setup client-side. See <see href="https://firebase.google.com/docs/cloud-messaging/customize-messages/localize-messages"/>
	/// </remarks>
	public Localization? BodyLocalization { get; set; } = null;

	/// <summary>
	/// Indicates whether the message should be sent with high priority.
	/// </summary>
	/// <remarks>
	/// Does not work with iOS devices, they will be sent as normal priority regardless of this setting. For more information see <see href="https://firebase.google.com/docs/cloud-messaging/customize-messages/setting-message-priority"/>
	/// </remarks>
	public bool HighPriority { get; set; } = false;

	/// <summary>
	/// How long the message is valid for in seconds - max is 28 days (2,419,200 seconds).
	/// </summary>
	/// <remarks>
	/// <para>Setting to 0 seconds will send the message immediately and discarded if it could not be delivered.</para>
	/// <para>Default (null) is 4 weeks by default.</para>
	/// <para>For more information see <see href="https://firebase.google.com/docs/cloud-messaging/customize-messages/setting-message-lifespan"/></para>
	/// </remarks>
	public TimeSpan? TimeToLive { get; set; }
}