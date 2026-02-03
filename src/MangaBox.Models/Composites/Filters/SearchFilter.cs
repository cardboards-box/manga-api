namespace MangaBox.Models.Composites.Filters;

/// <summary>
/// The base class for search filters
/// </summary>
/// <typeparam name="TOrderBy">The order by enum</typeparam>
public abstract class SearchFilter<TOrderBy>
	where TOrderBy : Enum
{
	private static readonly Random _rnd = new();

	/// <summary>
	/// The ID of the profile making the request
	/// </summary>
	[JsonIgnore]
	public Guid? ProfileId { get; set; }

	/// <summary>
	/// The page of results to fetch
	/// </summary>
	[JsonPropertyName("page")]
	public int Page { get; set; } = 1;

	/// <summary>
	/// The size of the results
	/// </summary>
	[JsonPropertyName("size")]
	public int Size { get; set; } = 100;

	/// <summary>
	/// The text to search for
	/// </summary>
	[JsonPropertyName("search")]
	public string? Search { get; set; }

	/// <summary>
	/// The IDs to include
	/// </summary>
	[JsonPropertyName("ids")]
	public Guid[] Ids { get; set; } = [];

	/// <summary>
	/// How to order the results
	/// </summary>
	[JsonPropertyName("orderBy")]
	public Dictionary<TOrderBy, bool> Order { get; set; } = [];

	/// <summary>
	/// Gets a random table suffix for temp tables
	/// </summary>
	/// <returns>The order key</returns>
	public static string TableSuffix(int length = 10)
	{
		var chars = "abcdefghijklmnopqrstuvwxyz";
		return new string([.. Enumerable.Range(0, length).Select(t => chars[_rnd.Next(chars.Length)])]);
	}

	/// <summary>
	/// Builds the query and outputs the parameters
	/// </summary>
	/// <param name="parameters">The parameters for the query</param>
	/// <returns>The query</returns>
	public abstract string Build(out DynamicParameters parameters);
}
