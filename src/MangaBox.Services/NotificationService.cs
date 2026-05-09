namespace MangaBox.Services;

using Imaging;
using Utilities.FCM;

/// <summary>
/// A service for sending push notifications
/// </summary>
public interface INotificationService
{
	/// <summary>
	/// Registers a device for a profile
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="deviceToken">The device token</param>
	/// <param name="name">The display name for the device</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the registration</returns>
	Task<Boxed> Register(Guid profileId, string deviceToken, string name, CancellationToken token);

	/// <summary>
	/// Unregisters a device from notifications
	/// </summary>
	/// <param name="deviceId">The ID of the device to delete</param>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The result of the un-registration</returns>
	Task<Boxed> Unregister(Guid deviceId, Guid profileId, CancellationToken token);

	/// <summary>
	/// Sends a notification regarding the given chapter
	/// </summary>
	/// <param name="chapterId">The ID of the chapter</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the notification was successful</returns>
	Task<bool> Chapter(Guid chapterId, CancellationToken token);

	/// <summary>
	/// Sends a notification regarding the given chapter
	/// </summary>
	/// <param name="chapter">The chapter</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the notification was successful</returns>
	Task<bool> Chapter(MbChapter chapter, CancellationToken token);

	/// <summary>
	/// Sends a notification regarding the given manga
	/// </summary>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the notification was successfull</returns>
	Task<bool> Manga(Guid mangaId, CancellationToken token);

	/// <summary>
	/// Sends a notification regarding the given manga
	/// </summary>
	/// <param name="manga">The manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the notification was successfull</returns>
	Task<bool> Manga(MangaBoxType<MbManga> manga, CancellationToken token);

	/// <summary>
	/// Sends a test notification
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the notification was successful</returns>
	Task<bool> Test(CancellationToken token);

	/// <summary>
	/// Subscribes the user to a given topic
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="id">The ID of the manga or person</param>
	/// <param name="manga">Whether the ID is for a manga or a person</param>
	/// <param name="token">The cancellation token of the request</param>
	/// <returns>The response</returns>
	Task<Boxed> Subscribe(Guid profileId, Guid id, bool manga, CancellationToken token);

	/// <summary>
	/// Unsubscribes the user from the given topic
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="id">The ID of the manga or person</param>
	/// <param name="manga">Whether the ID is for a manga or a person</param>
	/// <param name="token">The cancellation token of the request</param>
	/// <returns>The response</returns>
	Task<Boxed> Unsubscribe(Guid profileId, Guid id, bool manga, CancellationToken token);
}

internal class NotificationService(
	IDbService _db,
	IFcmService _fcm,
	IImageService _image) : INotificationService
{
	#region Topics & Notifications
	public static string ChapterTitle(MbChapter chapter)
	{
		var bob = new StringBuilder();
		if (chapter.Volume is not null)
			bob.Append($"Vol. {chapter.Volume} ");
		bob.Append($"Ch. {chapter.Ordinal} ");
		if (!string.IsNullOrEmpty(chapter.Title))
			bob.Append($"- {chapter.Title}");
		return bob.ToString().Trim();
	}

	public string ImageUrl(MbImage? image)
	{
		var imageValid = image is not null &&
			(image.LastFailedAt is null || 
			 image.LastFailedAt.Value + _image.ErrorWaitPeriod <= DateTime.UtcNow);
		
		if (!imageValid)
			return $"https://mangabox.app/logo.png";

		return $"https://v2.mangabox.app/image/{image!.Id}";
	}

	public Notification FromManga(MangaBoxType<MbManga> manga, bool test = false)
	{
		var cover = manga.GetItem<MbImage>();
		var message = test
			? "This is a test notification!"
			: "A new manga has been added to MangaBox!";
		return new()
		{
			Title = manga.Entity.Title,
			Body = message,
			ImageUrl = cover is null ? null : ImageUrl(cover),
			Data = new()
			{
				["route"] = $"/manga/{manga.Entity.Id}"
			}
		};
	}

	public Notification FromChapter(MbChapter chapter, MangaBoxType<MbManga> manga)
	{
		var cover = manga.GetItem<MbImage>();
		var title = ChapterTitle(chapter);
		return new()
		{
			Title = $"{manga.Entity.Title} - {title}",
			Body = $"A new chapter has been released!",
			ImageUrl = cover is null ? null : ImageUrl(cover),
			Data = new()
			{
				["route"] = $"/manga/{manga.Entity.Id}"
			}
		};
	}

	public static string MangaTopic(Guid id) => $"manga-{id}";

	public static string MangaTopic(MbManga manga) => MangaTopic(manga.Id);

	public static string MangaTopic(MangaBoxType<MbManga> manga) => MangaTopic(manga.Entity);

	public static string PersonTopic(Guid id) => $"person-{id}";

	public static string PersonTopic(MbPersonBase person) => PersonTopic(person.Id);

	public static IEnumerable<string> TopicsFromManga(MangaBoxType<MbManga> manga)
	{
		yield return MangaTopic(manga);

		var people = manga.GetItems<MbRelatedPerson>()
			.Where(t => t.Type != RelationshipType.Uploader);

		foreach (var person in people)
			yield return PersonTopic(person);
	}
	#endregion

	public async Task<bool> Test(CancellationToken token)
	{
		var manga = await _db.Manga.FetchWithRelationships(1);
		if (manga is null) return false;

		var notification = FromManga(manga, test: true);
		return await Send(manga, notification, token);
	}

	public async Task<bool> Chapter(Guid chapterId, CancellationToken token)
	{
		var fetch = await _db.Chapter.Fetch(chapterId);
		if (fetch is null) return false;

		return await Chapter(fetch, token);
	}

	public async Task<bool> Chapter(MbChapter chapter, CancellationToken token)
	{
		var manga = await _db.Manga.FetchWithRelationships(chapter.MangaId);
		if (manga is null) return false;

		var notification = FromChapter(chapter, manga);
		return await Send(manga, notification, token);
	}

	public async Task<bool> Manga(Guid mangaId, CancellationToken token)
	{
		var fetch = await _db.Manga.FetchWithRelationships(mangaId);
		if (fetch is null) return false;

		return await Manga(fetch, token);
	}

	public async Task<bool> Manga(MangaBoxType<MbManga> manga, CancellationToken token)
	{
		var notification = FromManga(manga);
		return await Send(manga, notification, token);
	}

	public async Task<bool> Send(MangaBoxType<MbManga> manga, Notification notification, CancellationToken token)
	{
		var results = await HandleSend(manga, notification, token).ToArrayAsync(token);
		var successes = results.Count(t => t.Success);

		var errors = results.Where(t => !t.Success).ToArray();
		if (errors.Length == 0)
			return successes > 0;

		await _db.Log.Insert(new()
		{
			LogLevel = MbLogLevel.Warning,
			Category = "Notifications - Send",
			Source = GetType().FullName ?? GetType().Name,
			Message = $"Failed to send notification for manga: {manga.Entity.Id}",
			Context = JsonSerializer.Serialize(errors),
		});

		return successes > 0;
	}

	public async IAsyncEnumerable<NotificationResult> HandleSend(
		MangaBoxType<MbManga> manga, Notification noti,
		[EnumeratorCancellation] CancellationToken token)
	{
		static bool IsUnregisterable(NotificationResult result)
		{
			if (result.TargetType != TargetType.Device ||
				result.Success ||
				result.ErrorReason is null) return false;

			FcmError[] unregisterable = 
			[
				FcmError.FCM_Unregistered,
				FcmError.FCM_SenderIdMismatch,
				FcmError.FCM_ApnsAuthError
			];
			return unregisterable.Contains(result.ErrorReason.Value);
		}

		var topics = TopicsFromManga(manga).ToArray();
		if (topics.Length > 0)
			await foreach (var result in _fcm.SendTopics(noti, topics, token))
				yield return result;

		var devices = await _db.NotificationDevice.GetDevicesForNotification(manga.Entity.Id);
		var tokens = devices.Select(t => t.FcmToken).ToArray();
		if (tokens.Length == 0) yield break;

		await foreach (var result in _fcm.SendDevices(noti, tokens, token))
		{
			if (!IsUnregisterable(result))
			{
				yield return result;
				continue;
			}

			var unregister = devices.Where(t => result.Targets.Contains(t.FcmToken));
			foreach(var device in unregister)
			{
				device.Active = false;
				await _db.NotificationDevice.Update(device);
				await _db.Log.Insert(new()
				{
					LogLevel = MbLogLevel.Warning,
					Category = "Notifications - Send - Unregister",
					Source = GetType().FullName ?? GetType().Name,
					Message = $"Device was unregistered because it received an error for device ID: {device.Id}",
					Context = JsonSerializer.Serialize(result),
				});
			}
			
			yield return result;
		}
	}

	public async Task<Boxed> Subscribe(Guid profileId, Guid id, bool isManga, CancellationToken token)
	{
		string topic;
		if (isManga)
		{
			var manga = await _db.Manga.FetchWithRelationships(id);
			if (manga is null) return Boxed.NotFound(nameof(MbManga), "The manga was not found");

			topic = MangaTopic(manga);
		}
		else
		{
			var person = await _db.Person.Fetch(id);
			if (person is null) return Boxed.NotFound(nameof(MbPerson), "The person was not found");

			topic = PersonTopic(person);
		}

		var subscription = new MbNotificationSubscription
		{
			ProfileId = profileId,
			MangaId = isManga ? id : null,
			PersonId = isManga ? null : id,
		};
		subscription.Id = await _db.NotificationSubscription.Upsert(subscription);

		var devices = await _db.NotificationDevice.FetchByProfile(profileId);
		if (devices.Length == 0)
			return Boxed.Ok(new SubjectSubscriptionResult(subscription, []));

		var tokens = devices
			.Where(t => t.Active)
			.Select(t => t.FcmToken)
			.ToArray();
		if (tokens.Length == 0)
			return Boxed.Ok(new SubjectSubscriptionResult(subscription, []));

		var errors = await _fcm.Subscribe(tokens, topic, token)
			.Where(t => !t.Success)
			.Select(t => new SubscriptionError(t.Error, t.Reason, t.Exception?.ToString()))
			.ToArrayAsync(token);

		if (errors.Length > 0)
			await _db.Log.Insert(new()
			{
				LogLevel = MbLogLevel.Warning,
				Category = "Notifications - Topic Subscription",
				Source = GetType().FullName ?? GetType().Name,
				Message = $"Failed to subscribe device to some topics during registration for profile: {profileId} -> {id} (manga: {isManga})",
				Context = JsonSerializer.Serialize(errors),
			});

		return Boxed.Ok(new SubjectSubscriptionResult(subscription, errors));
	}

	public async Task<Boxed> Unsubscribe(Guid profileId, Guid id, bool isManga, CancellationToken token)
	{
		string topic;
		Guid? mangaId = null;
		Guid? personId = null;
		if (isManga)
		{
			var manga = await _db.Manga.FetchWithRelationships(id);
			if (manga is null) return Boxed.NotFound(nameof(MbManga), "The manga was not found");

			topic = MangaTopic(manga);
			mangaId = manga.Entity.Id;
		}
		else
		{
			var person = await _db.Person.Fetch(id);
			if (person is null) return Boxed.NotFound(nameof(MbPerson), "The person was not found");

			topic = PersonTopic(person);
			personId = person.Id;
		}

		var subscription = await _db.NotificationSubscription.ClearSubscription(
			profileId, mangaId, personId);
		if (subscription is null)
			return Boxed.NotFound(nameof(MbNotificationSubscription), "The subscription was not found");

		var devices = await _db.NotificationDevice.FetchByProfile(profileId);
		if (devices.Length == 0)
			return Boxed.Ok(new SubjectSubscriptionResult(subscription, []));

		var tokens = devices
			.Where(t => t.Active)
			.Select(t => t.FcmToken)
			.ToArray();
		if (tokens.Length == 0)
			return Boxed.Ok(new SubjectSubscriptionResult(subscription, []));

		var errors = await _fcm.Unsubscribe(tokens, topic, token)
			.Where(t => !t.Success)
			.Select(t => new SubscriptionError(t.Error, t.Reason, t.Exception?.ToString()))
			.ToArrayAsync(token);

		if (errors.Length > 0)
			await _db.Log.Insert(new()
			{
				LogLevel = MbLogLevel.Warning,
				Category = "Notifications - Topic Subscription",
				Source = GetType().FullName ?? GetType().Name,
				Message = $"Failed to subscribe device to some topics during registration for profile: {profileId} -> {id} (manga: {isManga})",
				Context = JsonSerializer.Serialize(errors),
			});

		return Boxed.Ok(new SubjectSubscriptionResult(subscription, errors));
	}

	public async Task<Boxed> Register(Guid profileId, string deviceToken, string name, CancellationToken token)
	{
		var device = new MbNotificationDevice
		{
			ProfileId = profileId,
			FcmToken = deviceToken,
			Name = name,
			Active = true,
		};
		device.Id = await _db.NotificationDevice.Upsert(device);

		var subscriptions = await _db.NotificationSubscription.FetchByProfile(profileId);
		if (subscriptions.Length == 0)
			return Boxed.Ok(new DeviceSubscriptionResult(device, []));

		var topics = subscriptions
			.Where(t => t.MangaId is not null && t.DeletedAt is null)
			.Select(t => MangaTopic(t.MangaId!.Value))
			.Concat(subscriptions
				.Where(t => t.PersonId is not null && t.DeletedAt is null)
				.Select(t => PersonTopic(t.PersonId!.Value))
			)
			.ToArray();

		var errors = topics.Length == 0
			? []
			: await _fcm.Subscribe(deviceToken, topics, token)
				.Where(t => !t.Success)
				.Select(t => new SubscriptionError(t.Error, t.Reason, t.Exception?.ToString()))
				.ToArrayAsync(token);

		if (errors.Length > 0)
			await _db.Log.Insert(new()
			{
				LogLevel = MbLogLevel.Warning,
				Category = "Notifications - Device Registration",
				Source = GetType().FullName ?? GetType().Name,
				Message = $"Failed to subscribe device to some topics during registration for profile: {profileId}",
				Context = JsonSerializer.Serialize(errors),
			});

		return Boxed.Ok(new DeviceSubscriptionResult(device, errors));
	}

	public async Task<Boxed> Unregister(Guid deviceId, Guid profileId, CancellationToken token)
	{
		var device = await _db.NotificationDevice.Fetch(deviceId);
		if (device is null || device.ProfileId != profileId)
			return Boxed.NotFound(nameof(MbNotificationDevice), "The device was not found");

		device.DeletedAt = DateTime.UtcNow;
		await _db.NotificationDevice.Update(device);

		var subscriptions = await _db.NotificationSubscription.FetchByProfile(profileId);
		if (subscriptions.Length == 0)
			return Boxed.Ok(new DeviceSubscriptionResult(device, []));

		var topics = subscriptions
			.Where(t => t.MangaId is not null && t.DeletedAt is null)
			.Select(t => MangaTopic(t.MangaId!.Value))
			.Concat(subscriptions
				.Where(t => t.PersonId is not null && t.DeletedAt is null)
				.Select(t => PersonTopic(t.PersonId!.Value))
			)
			.ToArray();

		var errors = topics.Length == 0
			? []
			: await _fcm.Unsubscribe(device.FcmToken, topics, token)
				.Where(t => !t.Success)
				.Select(t => new SubscriptionError(t.Error, t.Reason, t.Exception?.ToString()))
				.ToArrayAsync(token);

		if (errors.Length > 0)
			await _db.Log.Insert(new()
			{
				LogLevel = MbLogLevel.Warning,
				Category = "Notifications - Device UnRegistration",
				Source = GetType().FullName ?? GetType().Name,
				Message = $"Failed to unsubscribe device from some topics during unregistration for profile: {profileId}",
				Context = JsonSerializer.Serialize(errors),
			});

		return Boxed.Ok(new DeviceSubscriptionResult(device, errors));
	}
}

/// <summary>
/// Represents an error message from a subscription attempt
/// </summary>
/// <param name="Code">The error code</param>
/// <param name="Reason">The error reason</param>
/// <param name="Exception">The error exception</param>
public record class SubscriptionError(
	[property: JsonPropertyName("code")] FcmError? Code,
	[property: JsonPropertyName("reason")] string? Reason,
	[property: JsonPropertyName("exception")] string? Exception);

/// <summary>
/// The result of a device subscription attempt
/// </summary>
/// <param name="Device">The device that was created</param>
/// <param name="Errors">Any error that occurred while subscribing</param>
public record class DeviceSubscriptionResult(
	[property: JsonPropertyName("device")] MbNotificationDevice Device,
	[property: JsonPropertyName("errors")] SubscriptionError[] Errors);

/// <summary>
/// The result of a subject subscription attempt
/// </summary>
/// <param name="Subject">The subject that was created</param>
/// <param name="Errors">Any error that occurred while subscribing</param>
public record class SubjectSubscriptionResult(
	[property: JsonPropertyName("subject")] MbNotificationSubscription Subject,
	[property: JsonPropertyName("errors")] SubscriptionError[] Errors);
