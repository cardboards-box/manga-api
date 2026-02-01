namespace MangaBox.Models;

/// <summary>
/// Represents a person that does things
/// </summary>
[Table("mb_people")]
[InterfaceOption(nameof(MbPerson))]
public class MbPerson : MbPersonBase { }

/// <summary>
/// The base data for a MB person
/// </summary>
public abstract class MbPersonBase : MbDbObject
{
	/// <summary>
	/// The person's name
	/// </summary>
	[Column("name", Unique = true)]
	[JsonPropertyName("name"), MinLength(1)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The URL of the person's avatar
	/// </summary>
	[Column("avatar")]
	[MaxLength(2048), Url]
	[JsonPropertyName("avatar")]
	public string? Avatar { get; set; }

	/// <summary>
	/// Whether or not the person is an artist of a manga
	/// </summary>
	[Column("artist", ExcludeUpdates = true)]
	[JsonPropertyName("artist")]
	public bool Artist { get; set; } = false;

	/// <summary>
	/// Whether or not the person is an author of a manga
	/// </summary>
	[Column("author", ExcludeUpdates = true)]
	[JsonPropertyName("author")]
	public bool Author { get; set; } = false;

	/// <summary>
	/// Whether or not the person is a user of MB
	/// </summary>
	[Column("is_user", ExcludeUpdates = true)]
	[JsonPropertyName("user")]
	public bool User { get; set; } = false;

	/// <summary>
	/// The ID of the user's MB profile, if applicable
	/// </summary>
	[Column("profile_id", ExcludeUpdates = true), Fk<MbProfile>(ignore: true)]
	[JsonPropertyName("profileId")]
	public Guid? ProfileId { get; set; }
}