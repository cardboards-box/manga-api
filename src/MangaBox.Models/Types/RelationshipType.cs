namespace MangaBox.Models.Types;

/// <summary>
/// Represents the type of <see cref="MbMangaRelationship"/> that can exist
/// </summary>
public enum RelationshipType
{
	/// <summary>
	/// The person is the author of the manga
	/// </summary>
	Author,
	/// <summary>
	/// The person is the artist of the manga
	/// </summary>
	Artist,
	/// <summary>
	/// The person uploaded the manga
	/// </summary>
	Uploader,
}
