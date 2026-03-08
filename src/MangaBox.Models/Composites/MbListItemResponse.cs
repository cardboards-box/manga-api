namespace MangaBox.Models.Composites;

using Types;

/// <summary>
/// The response to creating/deleting a list item
/// </summary>
public class MbListItemResponse
{
	/// <summary>
	/// The error returned if any
	/// </summary>
	public MbListError? Error { get; set; }

	/// <summary>
	/// The ID of the list item
	/// </summary>
	public Guid? Id { get; set; }
}
