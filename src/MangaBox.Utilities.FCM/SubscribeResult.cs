namespace MangaBox.Utilities.FCM;

/// <summary>
/// The result of a subscription / unsubscription request
/// </summary>
/// <param name="Topic">The topic that was subscribed to</param>
/// <param name="Devices">The devices that were subscribed</param>
/// <param name="Error">The error code if the subscription failed</param>
/// <param name="Reason">The reason slug if the subscription failed</param>
/// <param name="Exception">The exception that was thrown</param>
public record class SubscribeResult(
	string Topic,
	IReadOnlyList<string> Devices,
	FcmError? Error = null,
	string? Reason = null,
	Exception? Exception = null)
{
	/// <summary>
	/// Whether or not the subscription was successful
	/// </summary>
	public bool Success => Error is null && string.IsNullOrWhiteSpace(Reason);

	internal static SubscribeResult Valid(string topic, IReadOnlyList<string> devices) => new(topic, devices);

	internal static SubscribeResult InvalidTopic(string topic, IReadOnlyList<string> devices)
		=> new(topic, devices, FcmError.InvalidTopic, "invalid-topic");

	internal static SubscribeResult ExceptionOccurred(Exception ex, string topic, IReadOnlyList<string> devices, FcmError? error = null)
		=> new(topic, devices, Exception: ex, Error: error ?? FcmError.UnhandledException);
}
