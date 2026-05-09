using FirebaseAdmin.Messaging;

namespace MangaBox.Utilities.FCM;

/// <summary>
/// The result of a notification send operation
/// </summary>
/// <param name="Targets">All of the targets for the notification</param>
/// <param name="TargetType">The type of target for the notification</param>
/// <param name="ResponseId">The response ID from FCM, if any</param>
/// <param name="Exception">The error that occurred, if any</param>
/// <param name="ErrorReason">The type of error that occurred, if any</param>
public record class NotificationResult(
	[property: JsonPropertyName("targets")] ICollection<string> Targets,
	[property: JsonPropertyName("type")] TargetType TargetType,
	[property: JsonPropertyName("responseId")]
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	string? ResponseId = null,
	[property: JsonIgnore] Exception? Exception = null,
	[property: JsonPropertyName("reason")]
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	FcmError? ErrorReason = null)
{
	/// <summary>
	/// Indicates whether the notification was sent successfully
	/// </summary>
	[JsonIgnore]
	public bool Success => ErrorReason is null && Exception is null;

	/// <summary>
	/// The error message from the exception, if any
	/// </summary>
	[JsonPropertyName("stackTrace")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? StackTrace { get; set; } = Exception?.ToString();

	internal static NotificationResult Valid(string repsonseId, TargetType type, params string[] targets)
	{
		return new(targets, type, repsonseId);
	}

	internal static NotificationResult InvalidTopic(string topic)
	{
		return new([topic], TargetType.Topic, ErrorReason: FcmError.InvalidTopic);
	}

	internal static NotificationResult ExceptionOccurred(Exception ex, TargetType type, params string[] targets)
	{
		return new(targets, type, null, ex, FcmError.UnhandledException);
	}

	internal static NotificationResult ErrorOccurred(FcmError error, FirebaseMessagingException ex, TargetType type, params string[] targets)
	{
		return new(targets, type, null, ex, error);
	}
}