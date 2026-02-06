namespace MangaBox.Match.SauceNao;

/// <summary>
/// The user making the request
/// </summary>
public class SauceHeader
{
	/// <summary>
	/// The ID of the user making the request
	/// </summary>
	[JsonPropertyName("user_id")]
	public string? UserId { get; set; }

	/// <summary>
	/// The type of account being used
	/// </summary>
	[JsonPropertyName("account_type")]
	public string? AccountType { get; set; }

	/// <summary>
	/// The rate-limit in the short term
	/// </summary>
	[JsonPropertyName("short_limit")]
	public string ShortLimit { get; set; } = string.Empty;

	/// <summary>
	/// The rate-limit in the long term
	/// </summary>
	[JsonPropertyName("long_limit")]
	public string LongLimit { get; set; } = string.Empty;

	/// <summary>
	/// How many requests are remaining in the long-limit
	/// </summary>
	[JsonPropertyName("long_remaining")]
	public int LongRemaining { get; set; }

	/// <summary>
	/// How many requests are remaining in the short-limit
	/// </summary>
	[JsonPropertyName("short_remaining")]
	public int ShortRemaining { get; set; }

	/// <summary>
	/// The status of the user's account
	/// </summary>
	[JsonPropertyName("status")]
	public int Status { get; set; }

	/// <summary>
	/// The number of results requested
	/// </summary>
	[JsonPropertyName("results_requested")]
	public int ResultsRequested { get; set; }

	/// <summary>
	/// The databases that were used and their statuses
	/// </summary>
	[JsonPropertyName("index")]
	public Dictionary<string, SauceDatabase> Index { get; set; } = [];

	/// <summary>
	/// The depth of the search
	/// </summary>
	[JsonPropertyName("search_depth")]
	public string SearchDepth { get; set; } = string.Empty;

	/// <summary>
	/// The minimum similarity rating being used
	/// </summary>
	[JsonPropertyName("minimum_similarity")]
	public double MinimumSimilarity { get; set; }

	/// <summary>
	/// The display image URL being queried
	/// </summary>
	[JsonPropertyName("query_image_display")]
	public string QueryImageDisplay { get; set; } = string.Empty;

	/// <summary>
	/// The image URL being queried
	/// </summary>
	[JsonPropertyName("query_image")]
	public string QueryImage { get; set; } = string.Empty;

	/// <summary>
	/// The number of results returned
	/// </summary>
	[JsonPropertyName("results_returned")]
	public int ResultsReturned { get; set; }

	/// <summary>
	/// The message of the results
	/// </summary>
	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;
}
