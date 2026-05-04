namespace MangaBox.Models;

/// <summary>
/// Represents a list of manga that a user has created
/// </summary>
[Table("mb_lists")]
[InterfaceOption(nameof(MbList))]
[Searchable(nameof(Name), nameof(Description))]
public class MbList : MbDbObject
{
	/// <summary>
	/// The ID of the profile that owns this list
	/// </summary>
	[Column("profile_id", Unique = true), Fk<MbProfile>]
	[JsonPropertyName("profileId")]
	public Guid ProfileId { get; set; }

	/// <summary>
	/// The name of the list
	/// </summary>
	[Column("name", Unique = true)]
	[JsonPropertyName("name"), Required, MinLength(1)]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The list this list was cloned from, if any
	/// </summary>
	[Column("cloned_from"), Fk<MbList>]
	[JsonPropertyName("clonedFrom")]
	public Guid? ClonedFrom { get; set; }

	/// <summary>
	/// The optional description of the list
	/// </summary>
	[Column("description")]
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>
	/// Whether or not the list is public
	/// </summary>
	[Column("is_public")]
	[JsonPropertyName("isPublic")]
	public bool IsPublic { get; set; } = false;

	/// <summary>
	/// The request to create a new list
	/// </summary>
	/// <param name="Name">The name of the list</param>
	/// <param name="Description">The description of the list</param>
	/// <param name="IsPublic">Whether the list is public</param>
	public record class ListCreate(
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("description")] string? Description,
		[property: JsonPropertyName("isPublic")] bool IsPublic);

	/// <summary>
	/// The request to update an existing list
	/// </summary>
	/// <param name="Description">The new description of the list</param>
	/// <param name="IsPublic">Whether the list is public</param>
	public record class ListUpdate(
		[property: JsonPropertyName("description")] string? Description,
		[property: JsonPropertyName("isPublic")] bool IsPublic);

	/// <summary>
	/// Imports a list from the MD Api
	/// </summary>
	/// <param name="MdListId">The ID of the list on MangaDex</param>
	/// <param name="Name">The optional name to use for the list</param>
	/// <param name="Description">The description of the list</param>
	/// <param name="IsPublic">Whether the list is public</param>
	public record class ListImportMD(
		[property: JsonPropertyName("mdListId")] string MdListId,
		[property: JsonPropertyName("isPublic")] bool IsPublic,
		[property: JsonPropertyName("name")] string? Name,
		[property: JsonPropertyName("description")] string? Description);
}
