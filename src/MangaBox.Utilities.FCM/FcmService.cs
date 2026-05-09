using FirebaseAdmin.Messaging;

namespace MangaBox.Utilities.FCM;

using NotiResult = IAsyncEnumerable<NotificationResult>;
using SubResult = IAsyncEnumerable<SubscribeResult>;

/// <summary>
/// A service for interfacing with Firebase Cloud Messaging for sending push notifications
/// </summary>
public interface IFcmService
{
	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="condition">The condition to send the notification to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendCondition(Notification notification, string condition, CancellationToken token = default)
		=> SendConditions(notification, [condition], token);

	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="conditions">The conditions to send the notification to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendConditions(Notification notification, IEnumerable<string> conditions, CancellationToken token = default);

	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="deviceToken">The user device token to send the message to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendDevice(Notification notification, string deviceToken, CancellationToken token = default)
		=> SendDevices(notification, [deviceToken], token);

	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="deviceTokens">The user device tokens to send the message to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendDevices(Notification notification, IEnumerable<string> deviceTokens, CancellationToken token = default);

	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="topic">The topic to send the notification to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendTopic(Notification notification, string topic, CancellationToken token = default)
		=> SendTopics(notification, [topic], token);

	/// <summary>
	/// Sends a push notification to the specified targets
	/// </summary>
	/// <param name="notification">The notification to send</param>
	/// <param name="topics">The topics to send the notification to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	NotiResult SendTopics(Notification notification, IEnumerable<string> topics, CancellationToken token = default);

	/// <summary>
	/// Subscribe the given devices to the given topics
	/// </summary>
	/// <param name="devices">The device tokens to manage</param>
	/// <param name="topics">The topics to subscribe to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the subscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Subscribe(IReadOnlyList<string> devices, IEnumerable<string> topics, CancellationToken token = default);

	/// <summary>
	/// Subscribe the given device to the given topic
	/// </summary>
	/// <param name="device">The device token to manage</param>
	/// <param name="topic">The topic to subscribe to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the subscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Subscribe(string device, string topic, CancellationToken token = default)
		=> Subscribe([device], [topic], token);

	/// <summary>
	/// Subscribe the given device to the given topics
	/// </summary>
	/// <param name="device">The device token to manage</param>
	/// <param name="topics">The topics to subscribe to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the subscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Subscribe(string device, IReadOnlyList<string> topics, CancellationToken token = default)
		=> Subscribe([device], topics, token);

	/// <summary>
	/// Subscribe the given devices to the given topic
	/// </summary>
	/// <param name="devices">The device tokens to manage</param>
	/// <param name="topic">The topic to subscribe to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the subscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Subscribe(IReadOnlyList<string> devices, string topic, CancellationToken token = default)
		=> Subscribe(devices, [topic], token);

	/// <summary>
	/// Unsubscribe the given devices to the given topics
	/// </summary>
	/// <param name="devices">The device tokens to manage</param>
	/// <param name="topics">The topics to unsubscribe from</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the unsubscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Unsubscribe(IReadOnlyList<string> devices, IEnumerable<string> topics, CancellationToken token = default);

	/// <summary>
	/// Unsubscribe the given device from the given topic
	/// </summary>
	/// <param name="device">The device token to manage</param>
	/// <param name="topic">The topic to unsubscribe from</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the unsubscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Unsubscribe(string device, string topic, CancellationToken token = default)
		=> Unsubscribe([device], [topic], token);

	/// <summary>
	/// Unsubscribe the given device from the given topic
	/// </summary>
	/// <param name="device">The device token to manage</param>
	/// <param name="topics">The topic to unsubscribe from</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the unsubscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Unsubscribe(string device, IReadOnlyList<string> topics, CancellationToken token = default)
		=> Unsubscribe([device], topics, token);

	/// <summary>
	/// Unsubscribe the given devices from the given topic
	/// </summary>
	/// <param name="devices">The device tokens to manage</param>
	/// <param name="topic">The topic to unsubscribe from</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the unsubscriptions</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	SubResult Unsubscribe(IReadOnlyList<string> devices, string topic, CancellationToken token = default)
		=> Unsubscribe(devices, [topic], token);

	/// <summary>
	/// Validate that the topic name is valid.
	/// Needs to match the given regex: ^[a-zA-Z0-9-_.~%]{1,900}$
	/// </summary>
	/// <param name="topic">The topic name to validate</param>
	/// <returns>Whether or not the topic name is valid</returns>
	bool ValidTopic(string topic);
}

/// <inheritdoc cref="IFcmService"/>
internal partial class FcmService(
	FcmConnection _fcm,
	ILogger<FcmService> _logger) : IFcmService
{
	#region Notification Sending
	/// <summary>
	/// The maximum number of topics that can be included in a single condition
	/// </summary>
	/// <remarks><see href="https://firebase.google.com/docs/cloud-messaging/send-topic-messages#sending-to-topic-conditions"/></remarks>
	public const int MaxTopicsPerCondition = 5;

	/// <summary>
	/// The maximum number of device tokens that can be included in a single batch send
	/// </summary>
	/// <remarks><see href="https://firebase.google.com/docs/cloud-messaging/send/admin-sdk#send-messages-to-multiple-devices"/></remarks>
	public const int MaxDevicesPerBatch = 500;

	/// <summary>
	/// The maximum number of devices that can be subscribed to a single topic
	/// </summary>
	/// <remarks><see href="https://firebase.google.com/docs/cloud-messaging/manage-topic-subscriptions#manage-topic-subscriptions-admin-sdk"/></remarks>
	public const int MaxDevicesPerSubscription = 1000;

	/// <summary>
	/// Build the FCM notification from the given notification data
	/// </summary>
	/// <param name="msg">The notification data to send</param>
	/// <returns>The message to send</returns>
	public static Message FromNotification(Notification msg)
	{
		AndroidConfig? GetAndroid()
		{
			if (msg.TitleLocalization is null &&
				msg.BodyLocalization is null &&
				msg.TimeToLive is null &&
				!msg.HighPriority)
				return null;

			return new AndroidConfig
			{
				Priority = msg.HighPriority ? Priority.High : Priority.Normal,
				TimeToLive = msg.TimeToLive,
				Notification = msg.TitleLocalization is null && msg.BodyLocalization is null ? null : new()
				{
					TitleLocKey = msg.TitleLocalization?.Key,
					TitleLocArgs = msg.TitleLocalization?.Arguments ?? [],
					BodyLocKey = msg.BodyLocalization?.Key,
					BodyLocArgs = msg.BodyLocalization?.Arguments ?? [],
				},
			};
		}

		ApnsConfig? GetAPNs()
		{
			if (msg.TitleLocalization is null &&
				msg.BodyLocalization is null &&
				msg.TimeToLive is null)
				return null;

			//APNS requires a unix epoch timestamp for expiration (seconds)
			var ttl = !msg.TimeToLive.HasValue ? null :
				((int)DateTime.UtcNow
					.Add(msg.TimeToLive.Value)
					.Subtract(DateTime.UnixEpoch).TotalSeconds)
					.ToString();

			return new ApnsConfig
			{
				Headers = ttl is null ? null : new Dictionary<string, string>()
				{
					["apns-expiration"] = ttl,
				},
				Aps = msg.TitleLocalization is null && msg.BodyLocalization is null ? null : new()
				{
					Alert = new()
					{
						TitleLocKey = msg.TitleLocalization?.Key,
						TitleLocArgs = msg.TitleLocalization?.Arguments ?? [],
						LocKey = msg.BodyLocalization?.Key,
						LocArgs = msg.BodyLocalization?.Arguments ?? [],
					},
				},
			};
		}

		return new Message
		{
			Notification = new()
			{
				Title = msg.Title,
				Body = msg.Body,
				ImageUrl = msg.ImageUrl,
			},
			Data = msg.Data ?? [],
			Android = GetAndroid(),
			Apns = GetAPNs(),
		};
	}

	/// <summary>
	/// Convert a <see cref="MessagingErrorCode"/> to an internal <see cref="FcmError"/> when a corrective action is available
	/// </summary>
	/// <param name="code">The error code from FCM</param>
	/// <returns>The internal error code</returns>
	public static FcmError FromMessagingError(MessagingErrorCode? code)
	{
		//Only handle specific errors we have corrective actions for
		//https://firebase.google.com/docs/reference/fcm/rest/v1/ErrorCode
		return code switch
		{
			MessagingErrorCode.ThirdPartyAuthError => FcmError.FCM_ApnsAuthError,
			MessagingErrorCode.InvalidArgument => FcmError.FCM_InvalidArgument,
			MessagingErrorCode.QuotaExceeded => FcmError.FCM_QuotaExceeded,
			MessagingErrorCode.SenderIdMismatch => FcmError.FCM_SenderIdMismatch,
			MessagingErrorCode.Unregistered => FcmError.FCM_Unregistered,
			_ => FcmError.UnhandledException,
		};
	}

	/// <summary>
	/// Builds an or conditional for the given topics
	/// </summary>
	/// <param name="topics">The topics to combine into one condition</param>
	/// <returns>The conditional</returns>
	public static string BuildTopicConditional(IEnumerable<string> topics)
	{
		return string.Join(" || ", topics.Select(t => $"'{t}' in topics"));
	}

	/// <summary>
	/// Sends the given single-target message to FCM
	/// </summary>
	/// <param name="message">The message to send to FCM</param>
	/// <param name="type">The type of target to send the notification to</param>
	/// <param name="targets">All of the target values to send to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the message send</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	public async Task<NotificationResult> SendMessage(Message message, TargetType type, string[] targets, CancellationToken token)
	{
		try
		{
			var response = await _fcm.Instance.SendAsync(message, token);
			var result = NotificationResult.Valid(response, type, targets);
			return result;
		}
		catch (FirebaseMessagingException ex)
		{
			var code = FromMessagingError(ex.MessagingErrorCode);
			_logger.LogError(ex, "FirebaseMessagingException occurred while sending {Type} notification", type);
			return NotificationResult.ErrorOccurred(code, ex, type, targets);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An unhandled exception occurred while sending {Type} notification", type);
			return NotificationResult.ExceptionOccurred(ex, type, targets);
		}
	}

	/// <summary>
	/// Sends the given message to a batch of device tokens
	/// </summary>
	/// <param name="message">The notification to send</param>
	/// <param name="deviceTokens">The device tokens to send the message to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the message send</returns>
	/// <remarks>Only <see cref="OperationCanceledException"/> is bubbled up, everything else is captured</remarks>
	public async Task<IEnumerable<NotificationResult>> SendDeviceBatch(Message message, IReadOnlyList<string> deviceTokens, CancellationToken token)
	{
		IEnumerable<NotificationResult> IterateResults(BatchResponse response)
		{
			for (var i = 0; i < response.Responses.Count; i++)
			{
				var resp = response.Responses[i];
				var token = deviceTokens[i];
				if (resp.IsSuccess)
				{
					yield return NotificationResult.Valid(resp.MessageId!, TargetType.Device, token);
					continue;
				}

				var code = FromMessagingError(resp.Exception?.MessagingErrorCode);
				yield return NotificationResult.ErrorOccurred(code, resp.Exception!, TargetType.Device, token);
			}
		}

		try
		{
			var multicast = new MulticastMessage
			{
				Notification = message.Notification,
				Data = message.Data,
				Tokens = deviceTokens,
				Android = message.Android,
				Apns = message.Apns,
			};
			var response = await _fcm.Instance.SendEachForMulticastAsync(multicast, token);
			return IterateResults(response);
		}
		catch (FirebaseMessagingException ex)
		{
			var code = FromMessagingError(ex.MessagingErrorCode);
			_logger.LogError(ex, "FirebaseMessagingException occurred while sending notification to devices");
			return [NotificationResult.ErrorOccurred(code, ex, TargetType.Device, [.. deviceTokens])];
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An unhandled exception occurred while sending notification to devices");
			return [NotificationResult.ExceptionOccurred(ex, TargetType.Device, [.. deviceTokens])];
		}
	}

	public async NotiResult SendTopics(Notification notification, IEnumerable<string> topics, [EnumeratorCancellation] CancellationToken token = default)
	{
		var msg = FromNotification(notification);
		var badTopics = new List<string>();
		foreach (var chunk in Chunk(topics, MaxTopicsPerCondition, badTopics))
		{
			if (chunk.Length == 0) continue;

			if (chunk.Length == 1) msg.Topic = chunk[0];
			else msg.Condition = BuildTopicConditional(chunk);

			var result = await SendMessage(msg, TargetType.Topic, chunk, token);
			yield return result;
		}

		foreach (var bad in badTopics)
			yield return NotificationResult.InvalidTopic(bad);
	}

	public async NotiResult SendConditions(Notification notification, IEnumerable<string> conditions, [EnumeratorCancellation] CancellationToken token = default)
	{
		var msg = FromNotification(notification);
		foreach (var condition in conditions)
		{
			msg.Condition = condition;
			yield return await SendMessage(msg, TargetType.Condition, [condition], token);
		}
	}

	public async NotiResult SendDevices(Notification notification, IEnumerable<string> deviceTokens, [EnumeratorCancellation] CancellationToken token = default)
	{
		var msg = FromNotification(notification);
		foreach (var chunk in Chunk(deviceTokens, MaxDevicesPerBatch))
		{
			var results = await SendDeviceBatch(msg, chunk, token);
			foreach (var result in results)
				yield return result;
		}
	}
	#endregion

	#region Topic Subscription
	/// <summary>
	/// Convert the subscription error info to an internal <see cref="FcmError"/>
	/// </summary>
	/// <param name="error">The error that occurred</param>
	/// <returns>The internal error code</returns>
	public static FcmError FromSubscribeError(ErrorInfo error)
	{
		return error.Reason switch
		{
			"invalid-argument" => FcmError.FCM_InvalidArgument,
			"registration-token-not-registered" => FcmError.FCM_Unregistered,
			"too-many-topics" => FcmError.FCM_TooManyTopics,
			_ => FcmError.UnhandledException
		};
	}

	/// <summary>
	/// Sends a request to subscribe or unsubscribe devices to/from a topic
	/// </summary>
	/// <param name="subscribe">Whether to subscribe or unsubscribe from the topic</param>
	/// <param name="topic">The topic to subscribe or unsubcribe to/from</param>
	/// <param name="devices">The devices to subscribe or unsubscribe</param>
	/// <returns>The result of the request</returns>
	public async Task<IEnumerable<SubscribeResult>> SendTopicManage(bool subscribe, string topic, IReadOnlyList<string> devices)
	{
		IEnumerable<SubscribeResult> IterateResults(TopicManagementResponse response)
		{
			var succeeded = new HashSet<string>(devices);
			foreach (var error in response.Errors)
			{
				var device = devices[error.Index];
				succeeded.Remove(device);
				var code = FromSubscribeError(error);
				var fake = new Exception($"Error subscribing/unsubscribing device '{device}' to/from topic '{topic}': {error.Reason}");
				yield return new(topic, [device], code, error.Reason, fake);
			}

			if (succeeded.Count > 0)
				yield return SubscribeResult.Valid(topic, [.. succeeded]);
		}

		try
		{
			var response = subscribe
				? await _fcm.Instance.SubscribeToTopicAsync(devices, topic)
				: await _fcm.Instance.UnsubscribeFromTopicAsync(devices, topic);
			return IterateResults(response);
		}
		catch (FirebaseMessagingException ex)
		{
			var code = FromMessagingError(ex.MessagingErrorCode);
			_logger.LogError(ex, "FirebaseMessagingException occurred while attempting to {Subscribe} to {Topic}",
				subscribe ? "Subscribe" : "Unsubscribe", topic);
			return [SubscribeResult.ExceptionOccurred(ex, topic, devices, code)];
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while attempting to {Subscribe} to {Topic}",
				subscribe ? "Subscribe" : "Unsubscribe", topic);
			return [SubscribeResult.ExceptionOccurred(ex, topic, devices)];
		}
	}

	/// <summary>
	/// Iterate through all of the topics and manage the subscriptions for the given devices
	/// </summary>
	/// <param name="subscribe">Whether or subscribe or unsubscribe from the topics</param>
	/// <param name="devices">The devices to manage</param>
	/// <param name="topics">The topics to manage</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The results for the subscriptions</returns>
	public async SubResult ManageTopic(bool subscribe, IReadOnlyList<string> devices, IEnumerable<string> topics, [EnumeratorCancellation] CancellationToken token)
	{
		var deviceChunks = Chunk(devices, MaxDevicesPerSubscription).ToArray();
		foreach (var topic in topics)
		{
			if (token.IsCancellationRequested)
				throw new OperationCanceledException("Cancellation was requested", token);

			if (!ValidTopic(topic))
			{
				yield return SubscribeResult.InvalidTopic(topic, devices);
				continue;
			}

			foreach (var chunk in deviceChunks)
			{
				var results = await SendTopicManage(subscribe, topic, chunk);
				foreach (var result in results)
					yield return result;
			}
		}
	}

	public SubResult Subscribe(IReadOnlyList<string> devices, IEnumerable<string> topics, CancellationToken token = default)
	{
		return ManageTopic(true, devices, topics, token);
	}

	public SubResult Unsubscribe(IReadOnlyList<string> devices, IEnumerable<string> topics, CancellationToken token = default)
	{
		return ManageTopic(false, devices, topics, token);
	}
	#endregion

	/// <summary>
	/// Chunk the given collection into smaller collections of the given size
	/// </summary>
	/// <param name="data">The data to chunk</param>
	/// <param name="size">The size of each smaller chunk</param>
	/// <param name="bad">A list of invalid topic names (only specify this when chunking topics)</param>
	/// <returns>The smaller chunks of data</returns>
	/// <remarks>
	/// <para>This method has a side effect:</para>
	/// <para>If you specify <paramref name="bad"/> it will validate each entry to ensure it's a valid FCM topic</para>
	/// <para>Any invalid FCM topic will be excluded from the returned chunks</para>
	/// </remarks>
	public IEnumerable<string[]> Chunk(IEnumerable<string> data, int size, List<string>? bad = null)
	{
		List<string> chunk = [];
		foreach (var item in data)
		{
			if (bad is not null && !ValidTopic(item))
			{
				bad.Add(item);
				continue;
			}

			chunk.Add(item);
			if (chunk.Count < size)
				continue;
			yield return [.. chunk];
			chunk.Clear();
		}

		if (chunk.Count > 0)
			yield return [.. chunk];
	}

	/// <summary>
	/// Validate that the topic name is valid.
	/// Needs to match the given regex: ^[a-zA-Z0-9-_.~%]{1,900}$
	/// </summary>
	/// <param name="topic">The topic name to validate</param>
	/// <returns>Whether or not the topic name is valid</returns>
	public bool ValidTopic(string topic) => ValidTopicRegex().IsMatch(topic);

	/// <summary>The Regex for validating topic names</summary>
	/// <remarks>
	/// <para>The valid regex for a topic name as per <see href="https://firebase.google.com/docs/cloud-messaging/send-message#send-messages-to-topics-legacy"/></para>
	/// <para>The documentation is for legacy topics, but the regex is still valid for the new topics and the `/topics/` prefix is not required anymore.</para>
	/// <para>Unfortunately, there is no official documentation outlining this but the return results from attempting invalid topic names do reflect this.</para>
	/// </remarks>
	[GeneratedRegex("^[a-zA-Z0-9-_.~%]{1,900}$")]
	private static partial Regex ValidTopicRegex();
}
