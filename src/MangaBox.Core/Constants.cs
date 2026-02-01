namespace MangaBox.Core;

/// <summary>
/// A collection of constant values for the application
/// </summary>
public static class Constants
{
	/// <summary>
	/// The application's name
	/// </summary>
	public const string APPLICATION_NAME = "MangaBox";

	/// <summary>
	/// The application's unqualified URL
	/// </summary>
	public const string APPLICATION_URL = "mangabox.app";

	/// <summary>
	/// The maximum length for relatively short strings
	/// </summary>
	/// <remarks>Things like usernames, keys, etc.</remarks>
	public const int MAX_NAME_LENGTH = 64;

	/// <summary>
	/// The minimum length for relatively short strings
	/// </summary>
	/// <remarks>Things like usernames, keys, etc.</remarks>
	public const int MIN_NAME_LENGTH = 2;

	/// <summary>
	/// The max length for URLs
	/// </summary>
	public const int MAX_URL_LENGTH = 2048;

	/// <summary>
	/// The max length for blobs of text
	/// </summary>
	public const int MAX_TEXT_LENGTH = 2048;

	/// <summary>
	/// The max length for comments
	/// </summary>
	public const int MAX_COMMENT_LENGTH = 10_000;

	/// <summary>
	/// The max length for description texts
	/// </summary>
	public const int MAX_DESCRIPTION_LENGTH = 256;

	/// <summary>
	/// The min length for description texts
	/// </summary>
	public const int MIN_DESCRIPTION_LENGTH = 3;

	/// <summary>
	/// The max number of IDs that can be requested at once
	/// </summary>
	public const int MAX_REQUEST_IDS = 100;

	/// <summary>
	/// The max number of commit history records to fetch
	/// </summary>
	public const int MAX_HISTORY_LIMIT = 100;

	/// <summary>
	/// The minimum page number for pagination
	/// </summary>
	public const int PAGINATION_PAGE_MIN = 1;

	/// <summary>
	/// The default page to use for pagination
	/// </summary>
	public const int PAGINATION_PAGE_DEFAULT = 1;

	/// <summary>
	/// The default amount of items to return per page in pagination
	/// </summary>
	public const int PAGINATION_SIZE_DEFAULT = 25;

	/// <summary>
	/// The maximum amount of items to return per page in pagination
	/// </summary>
	public const int PAGINATION_SIZE_MAX = 100;

	/// <summary>
	/// The minimum amount of items to return per page in pagination
	/// </summary>
	public const int PAGINATION_SIZE_MIN = 1;
}
