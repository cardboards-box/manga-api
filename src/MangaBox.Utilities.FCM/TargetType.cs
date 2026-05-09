namespace MangaBox.Utilities.FCM;

/// <summary>
/// The type of notification target to send to
/// </summary>
public enum TargetType
{
	/// <summary>
	/// Send to a specific device
	/// </summary>
	Device,
	/// <summary>
	/// Send to a topic
	/// </summary>
	Topic,
	/// <summary>
	/// Send to a condition
	/// </summary>
	Condition
}