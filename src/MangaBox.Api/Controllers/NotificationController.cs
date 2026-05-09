namespace MangaBox.Api.Controllers;

/// <summary>
/// The controller for notification endpoints
/// </summary>
public class NotificationController(
	IDbService _db,
	INotificationService _notifications,
	ILogger<NotificationController> logger) : BaseController(logger)
{
#if DEBUG
	/// <summary>
	/// Sends a test notification
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The user's devices</returns>
	[HttpGet, Route("notification/test"), ProducesBox<bool>]
	public Task<IActionResult> Test(CancellationToken token) => Box(async () =>
	{
		var result = await _notifications.Test(token);
		return Boxed.Ok(result);
	});
#endif

	/// <summary>
	/// Gets all of a user's registered devices
	/// </summary>
	/// <returns>The user's devices</returns>
	[HttpGet, Route("notification/device")]
	[ProducesArray<MbNotificationDevice>, ProducesError(401)]
	public Task<IActionResult> MyDevices() => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		var devices = await _db.NotificationDevice.FetchByProfile(pid.Value);
		return Boxed.Ok(devices);
	});

	/// <summary>
	/// Registers a device for push notifications using the specified device token.
	/// </summary>
	/// <param name="request">The request containing the device token to register.</param>
	/// <param name="token">A cancellation token that can be used to cancel the operation.</param>
	/// <returns>The results of the request</returns>
	[HttpPost, Route("notification/device")]
	[ProducesBox<DeviceSubscriptionResult>, ProducesError(401), ProducesError(400)]
	public Task<IActionResult> RegisterDevice(
		[FromBody] RegisterDeviceRequest request, 
		CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");
		if (string.IsNullOrWhiteSpace(request.Token))
			return Boxed.Bad("Device token is required.");
		if (string.IsNullOrWhiteSpace(request.Name))
			return Boxed.Bad("Device name is required.");
		return await _notifications.Register(pid.Value, request.Token, request.Name, token);
	});

	/// <summary>
	/// Unregisters a device for push notifications
	/// </summary>
	/// <param name="id">The ID of the device to unregister</param>
	/// <param name="token">A cancellation token that can be used to cancel the operation.</param>
	/// <returns>The results of the request</returns>
	[HttpDelete, Route("notification/device/{id}")]
	[ProducesBox<DeviceSubscriptionResult>, ProducesError(401), ProducesError(400)]
	public Task<IActionResult> UnRegisterDevice(
		[FromRoute] string id,
		CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");
		if (!Guid.TryParse(id, out var deviceId))
			return Boxed.Bad("Invalid Device ID");
		return await _notifications.Unregister(deviceId, pid.Value, token);
	});

	/// <summary>
	/// Gets all of a user's subscriptions
	/// </summary>
	/// <returns>The user's subscriptions</returns>
	[HttpGet, Route("notification/subscription")]
	[ProducesArray<MbNotificationSubscription>, ProducesError(401)]
	public Task<IActionResult> MySubscriptions() => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");
		var subs = await _db.NotificationSubscription.FetchByProfile(pid.Value);
		return Boxed.Ok(subs);
	});

	/// <summary>
	/// Subscribes to a manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	[HttpGet, Route("notification/manga/{id}")]
	[ProducesBox<SubjectSubscriptionResult>, ProducesError(401), ProducesError(400), ProducesError(404)]
	public Task<IActionResult> SubscribeManga([FromRoute] string id, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		if (!Guid.TryParse(id, out var vid))
			return Boxed.Bad("Invalid ID");

		return await _notifications.Subscribe(pid.Value, vid, true, token);
	});

	/// <summary>
	/// Unsubscribes from a manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	[HttpDelete, Route("notification/manga/{id}")]
	[ProducesBox<SubjectSubscriptionResult>, ProducesError(401), ProducesError(400), ProducesError(404)]
	public Task<IActionResult> UnsubscribeManga([FromRoute] string id, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		if (!Guid.TryParse(id, out var vid))
			return Boxed.Bad("Invalid ID");

		return await _notifications.Unsubscribe(pid.Value, vid, true, token);
	});

	/// <summary>
	/// Subscribes to a person
	/// </summary>
	/// <param name="id">The ID of the person</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	[HttpGet, Route("notification/person/{id}")]
	[ProducesBox<SubjectSubscriptionResult>, ProducesError(401), ProducesError(400), ProducesError(404)]
	public Task<IActionResult> SubscribePerson([FromRoute] string id, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		if (!Guid.TryParse(id, out var vid))
			return Boxed.Bad("Invalid ID");

		return await _notifications.Subscribe(pid.Value, vid, false, token);
	});

	/// <summary>
	/// Unsubscribes from a person
	/// </summary>
	/// <param name="id">The ID of the person</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	[HttpDelete, Route("notification/person/{id}")]
	[ProducesBox<SubjectSubscriptionResult>, ProducesError(401), ProducesError(400), ProducesError(404)]
	public Task<IActionResult> UnsubscribePerson([FromRoute] string id, CancellationToken token) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		if (!Guid.TryParse(id, out var vid))
			return Boxed.Bad("Invalid ID");

		return await _notifications.Unsubscribe(pid.Value, vid, false, token);
	});

	/// <summary>
	/// Updates the user's notification settings
	/// </summary>
	/// <param name="request">The notification setting options</param>
	/// <returns>The profile that was updated</returns>
	[HttpPost, Route("notification/settings")]
	[ProducesBox<MbProfile>, ProducesError(401)]
	public Task<IActionResult> UpdateSettings([FromBody] MbProfile.ProfileNotifications request) => Box(async () =>
	{
		var pid = this.GetProfileId();
		if (!pid.HasValue)
			return Boxed.Unauthorized("User is not authenticated.");

		var profile = await _db.Profile.Notifications(pid.Value, request);
		if (profile is null)
			return Boxed.NotFound(nameof(MbProfile), "The profile was not found");

		return Boxed.Ok(profile);
	});

	/// <summary>
	/// The request to register a device for notifications
	/// </summary>
	/// <param name="Token">The device token</param>
	/// <param name="Name">The display name of the device</param>
	public record class RegisterDeviceRequest(
		[property: JsonPropertyName("token")] string Token,
		[property: JsonPropertyName("name")] string Name);
}
