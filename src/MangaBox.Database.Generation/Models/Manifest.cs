namespace MangaBox.Database.Generation.Models;

/// <summary>
/// Represents a manifest of all of the files to be executed
/// </summary>
public class Manifest
{
    /// <summary>
    /// All of the scripts to run
    /// </summary>
    [JsonPropertyName("paths")]
    public string[] Paths { get; set; } = [];
}