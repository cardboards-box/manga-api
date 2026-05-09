namespace MangaBox.Database.Services;

using Models;
using Models.Composites;

/// <summary>
/// The service for interacting with the mb_notification_devices table
/// </summary>
public interface IMbNotificationDeviceDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_notification_devices table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbNotificationDevice?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_notification_devices table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbNotificationDevice item);

	/// <summary>
	/// Updates a record in the mb_notification_devices table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbNotificationDevice item);

	/// <summary>
	/// Inserts a record in the mb_notification_devices table if it doesn't exist, otherwise updates it
	/// </summary>
	/// <param name="item">The item to update or insert</param>
	/// <returns>The ID of the inserted/updated record</returns>
	Task<Guid> Upsert(MbNotificationDevice item);

	/// <summary>
	/// Gets all of the records from the mb_notification_devices table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbNotificationDevice[]> Get();

	/// <summary>
	/// Gets all of the devices that should receive a notification for the manga
	/// </summary>
	/// <param name="id">The ID of the manga</param>
	/// <returns>The devices that should receive a notification</returns>
	Task<MbNotificationDevice[]> GetDevicesForNotification(Guid id);

	/// <summary>
	/// Gets the devices by the Profile ID
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <returns>The devices associated with the profile</returns>
	Task<MbNotificationDevice[]> FetchByProfile(Guid profileId);
}

internal class MbNotificationDeviceDbService(
	IOrmService orm) : Orm<MbNotificationDevice>(orm), IMbNotificationDeviceDbService
{
	private static string? _queryByProfile;

	public Task<MbNotificationDevice[]> FetchByProfile(Guid profileId)
	{
		_queryByProfile ??= Map.Select(t => t.With(t => t.ProfileId).Null(t => t.DeletedAt));
		return Get(_queryByProfile, new { ProfileId = profileId });
	}

	public Task<MbNotificationDevice[]> GetDevicesForNotification(Guid id)
	{
		const string QUERY = """
			WITH has_subscription AS (
			    --Check if the user has subscribed to one of the authors/artists of the manga
			    SELECT ns.profile_id AS profile_id
			    FROM mb_people p
			    JOIN mb_manga_relationships r ON r.person_id = p.id
			    JOIN mb_notification_subscriptions ns ON ns.person_id = p.id
			    WHERE
			        r.manga_id = :id AND
			        p.deleted_at IS NULL AND
			        r.deleted_at IS NULL AND
			        ns.deleted_at IS NULL
			    UNION
			    --Check if the user has subscribed to the manga directly
			    SELECT ns.profile_id AS profile_id
			    FROM mb_manga m
			    JOIN mb_notification_subscriptions ns ON ns.manga_id = m.id
			    WHERE
			        m.id = :id AND
			        m.deleted_at IS NULL AND
			        ns.deleted_at IS NULL
			)
			SELECT
			    DISTINCT
			    nd.*
			FROM mb_manga m
			JOIN mb_manga_progress mp ON mp.manga_id = m.id
			JOIN mb_profiles p ON p.id = mp.profile_id
			JOIN mb_notification_devices nd ON nd.profile_id = p.id
			LEFT JOIN has_subscription hps ON hps.profile_id = p.id
			WHERE
			    m.id = :id AND
			    m.deleted_at IS NULL AND
			    mp.deleted_at IS NULL AND
			    p.deleted_at IS NULL AND
			    nd.deleted_at IS NULL AND
			    --Ensure the user doesn't have a topic based subscription
			    hps.profile_id IS NULL AND
			    --Ensure the device is active
			    nd.active = TRUE AND ((
			        --Check if the user is reading the manga and 
			        -- has enabled in-progress notifications
			        mp.last_read_at IS NOT NULL AND
			        mp.is_completed = FALSE AND
			        p.notify_in_progress = TRUE
			    ) OR (
			        --Check if the user has favourited the manga and 
			        -- has enabled favourite notifications
			        mp.favorited = TRUE AND
			        p.notify_favourites = TRUE
			    ));
			""";
		return Get(QUERY, new { id });
	}
}