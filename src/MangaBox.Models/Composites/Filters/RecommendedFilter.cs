namespace MangaBox.Models.Composites.Filters;

using Types;

/// <summary>
/// The filter for recommended manga
/// </summary>
public class RecommendedFilter
{
	/// <summary>
	/// The ratings to filter by
	/// </summary>
	[JsonPropertyName("ratings")]
	public ContentRating[] Ratings { get; set; } = [];

	/// <summary>
	/// Tags to include in the search
	/// </summary>
	[JsonPropertyName("tags")]
	public Guid[] Tags { get; set; } = [];

	/// <summary>
	/// Tags to exclude in the search
	/// </summary>
	[JsonPropertyName("tagsEx")]
	public Guid[] TagsEx { get; set; } = [];

	/// <summary>
	/// The size of the results
	/// </summary>
	[JsonPropertyName("size")]
	public int Size { get; set; } = 20;
}
