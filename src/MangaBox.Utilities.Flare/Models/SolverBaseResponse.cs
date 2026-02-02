namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// The base response for a solver request
/// </summary>
public abstract class SolverBaseResponse
{
    /// <summary>
    /// The status of the request
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The message of the request
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("startTimestamp")]
    internal long StartTimestampMilliseconds { get; set; }

    [JsonPropertyName("endTimestamp")]
    internal long EndTimestampMilliseconds { get; set; }

    /// <summary>
    /// When the request was started by flare
    /// </summary>
    [JsonIgnore]
    public DateTime StartTimestamp => DateTimeOffset.FromUnixTimeMilliseconds(StartTimestampMilliseconds).DateTime;

    /// <summary>
    /// When the request was finished by flare
    /// </summary>
    [JsonIgnore]
    public DateTime EndTimestamp => DateTimeOffset.FromUnixTimeMilliseconds(EndTimestampMilliseconds).DateTime;

    /// <summary>
    /// The version of the response
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
