namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// The response to requesting all of the sessions
/// </summary>
public class SolverSessionList : SolverBaseResponse
{
    /// <summary>
    /// The IDs of the sessions
    /// </summary>
    [JsonPropertyName("sessions")]
    public string[] SessionIds { get; set; } = [];
}
