namespace MangaBox.Models.Composites.Import;

/// <summary>
/// A name-value pair for importing attributes for manga, chapters, and pages
/// </summary>
public class ImportAttribute()
{
    /// <summary>
    /// The key / name of the attribute
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value of the attribute
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new ImportAttribute with the given name and value
    /// </summary>
    /// <param name="name">The key / name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    public ImportAttribute(string name, string value) : this()
    {
        Name = name;
        Value = value;
    }
}