using System;
using System.Collections.Generic;
using System.Text;

namespace MangaBox.Models.Types;

/// <summary>
/// Represents an error returned by the list item create/delete requests
/// </summary>
public enum MbListError
{
	/// <summary>
	/// The profile ID passed was null
	/// </summary>
	ProfileIdNull = 1,
	/// <summary>
	/// The profile ID was not found in the database
	/// </summary>
	ProfileMissing = 2,
	/// <summary>
	/// The list ID was not found in the database
	/// </summary>
	ListMissing = 3,
	/// <summary>
	/// The manga ID was not found in the database
	/// </summary>
	MangaMissing = 4,
	/// <summary>
	/// The profile doesn't own the list
	/// </summary>
	ProfileMismatch = 5,
	/// <summary>
	/// The list item doesn't exist
	/// </summary>
	ListItemMistmatch = 6,
}
