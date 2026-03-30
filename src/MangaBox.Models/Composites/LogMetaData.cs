namespace MangaBox.Models.Composites;

/// <summary>
/// The log meta-data
/// </summary>
/// <param name="Categories">The categories</param>
/// <param name="Sources">The sources</param>
public record class LogMetaData(
	[property: JsonPropertyName("categories")] string[] Categories,
	[property: JsonPropertyName("sources")] string[] Sources);
