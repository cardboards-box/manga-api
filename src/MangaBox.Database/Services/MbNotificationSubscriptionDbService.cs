namespace MangaBox.Database.Services;

using Models;

/// <summary>
/// The service for interacting with the mb_notification_subscriptions table
/// </summary>
public interface IMbNotificationSubscriptionDbService
{
	/// <summary>
	/// Fetches a record by its ID from the mb_notification_subscriptions table
	/// </summary>
	/// <param name="id">The ID of the record</param>
	/// <returns>The record</returns>
	Task<MbNotificationSubscription?> Fetch(Guid id);

	/// <summary>
	/// Inserts a record into the mb_notification_subscriptions table
	/// </summary>
	/// <param name="item">The item to insert</param>
	/// <returns>The ID of the inserted record</returns>
	Task<Guid> Insert(MbNotificationSubscription item);

	/// <summary>
	/// Updates a record in the mb_notification_subscriptions table
	/// </summary>
	/// <param name="item">The record to update</param>
	/// <returns>The number of records updated</returns>
	Task<int> Update(MbNotificationSubscription item);

	/// <summary>
	/// Inserts a record in the mb_notification_subscriptions table if it doesn't exist, otherwise updates it
	/// </summary>
	/// <param name="item">The item to update or insert</param>
	/// <returns>The ID of the inserted/updated record</returns>
	Task<Guid> Upsert(MbNotificationSubscription item);

	/// <summary>
	/// Gets all of the records from the mb_notification_subscriptions table
	/// </summary>
	/// <returns>All of the records</returns>
	Task<MbNotificationSubscription[]> Get();

	/// <summary>
	/// Gets the subscriptions by the Profile ID
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <returns>The subscriptions associated with the profile</returns>
	Task<MbNotificationSubscription[]> FetchByProfile(Guid profileId);

	/// <summary>
	/// Clears the subscription for the given IDs
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="mangaId">The ID of the manga</param>
	/// <param name="personId">The ID of the person</param>
	/// <returns>The subscription that was cleared</returns>
	Task<MbNotificationSubscription?> ClearSubscription(Guid profileId, Guid? mangaId, Guid? personId);
}

internal class MbNotificationSubscriptionDbService(
	IOrmService orm) : Orm<MbNotificationSubscription>(orm), IMbNotificationSubscriptionDbService
{
	private static string? _queryByProfile;

	public Task<MbNotificationSubscription[]> FetchByProfile(Guid profileId)
	{
		_queryByProfile ??= Map.Select(t => t.With(t => t.ProfileId).Null(t => t.DeletedAt));
		return Get(_queryByProfile, new { ProfileId = profileId });
	}

	public Task<MbNotificationSubscription?> ClearSubscription(Guid profileId, Guid? mangaId, Guid? personId)
	{
		const string QUERY = """
			SELECT * FROM mb_notification_subscriptions
			 WHERE 
				profile_id = :profileId AND
				deleted_at IS NULL AND (
					(:mangaId IS NOT NULL AND :mangaId = manga_id) OR
					(:personId IS NOT NULL AND :personId = person_id)
				);

			DELETE FROM mb_notification_subscriptions
			WHERE 
				profile_id = :profileId AND
				deleted_at IS NULL AND (
					(:mangaId IS NOT NULL AND :mangaId = manga_id) OR
					(:personId IS NOT NULL AND :personId = person_id)
				)
			""";
		return Fetch(QUERY, new { profileId, mangaId, personId });
	}
}