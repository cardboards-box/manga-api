namespace MangaBox.Models;

/// <summary>
/// The notification subscriptions for a profile
/// </summary>
[Table("mb_notification_subscriptions")]
public class MbNotificationSubscription : MbDbObject
{
	/// <summary>
	/// The ID of the profile that owns this subscription
	/// </summary>
	[Column("profile_id", Unique = true), Fk<MbProfile>]
	[JsonPropertyName("profileId")]
	public Guid ProfileId { get; set; }

	/// <summary>
	/// The ID of the manga this subscription is for
	/// </summary>
	[Column("manga_id", Unique = true), Fk<MbManga>]
	[JsonPropertyName("mangaId")]
	public Guid? MangaId { get; set; }

	/// <summary>
	/// The ID of the person this subscription is for
	/// </summary>
	[Column("person_id", Unique = true), Fk<MbPerson>]
	[JsonPropertyName("personId")]
	public Guid? PersonId { get; set; }
}
