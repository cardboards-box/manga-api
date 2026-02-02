namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// Represents a proxy to use for the solver request
/// </summary>
public class SolverProxy
{
    /// <summary>
    /// The URL of the proxy
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The username for the proxy
    /// </summary>
    [JsonPropertyName("username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; set; }

    /// <summary>
    /// The password for the proxy
    /// </summary>
    [JsonPropertyName("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
}
