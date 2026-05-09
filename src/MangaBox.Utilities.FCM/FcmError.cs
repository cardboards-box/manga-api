namespace MangaBox.Utilities.FCM;

/// <summary>
/// All of the various errors that can occur when sending a notification
/// </summary>
public enum FcmError
{
	/// <summary>
	/// The topic specified was invalid
	/// </summary>
	/// <remarks>Corrective action: Remove topic</remarks>
	InvalidTopic = 0,
	/// <summary>
	/// Indicates that an unhandled exception has occurred.
	/// </summary>
	/// <remarks>Corrective action: Figure out what the exception means... duh?</remarks>
	UnhandledException = 1,
	/// <summary>
	/// Apple Push Notification Service authentication error
	/// </summary>
	/// <remarks>Corrective action: Remove registration token</remarks>
	FCM_ApnsAuthError = 2,
	/// <summary>
	/// Indicates that an operation failed due to an invalid argument being provided.
	/// </summary>
	/// <remarks>Corrective action: Fix notification being sent</remarks>
	FCM_InvalidArgument = 3,
	/// <summary>
	/// Quota for sending messages has been exceeded
	/// </summary>
	/// <remarks>Corrective action: Resend request later</remarks>
	FCM_QuotaExceeded = 4,
	/// <summary>
	/// The sender app doesn't own the registration token
	/// </summary>
	/// <remarks>Corrective action: Remove registration token</remarks>
	FCM_SenderIdMismatch = 5,
	/// <summary>
	/// Registration token was unregistered
	/// </summary>
	/// <remarks>Corrective action: Remove registration token</remarks>
	FCM_Unregistered = 6,
	/// <summary>
	/// Registration token is subscribed to too many topics
	/// </summary>
	/// <remarks>Corrective action: Remove unused tokens</remarks>
	FCM_TooManyTopics = 7,
}
