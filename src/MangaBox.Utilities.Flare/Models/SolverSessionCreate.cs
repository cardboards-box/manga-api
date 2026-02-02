namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// The response to creating a session
/// </summary>
public class SolverSessionCreate : SolverBaseResponse
{
    /// <summary>
    /// The ID of the session that was created
    /// </summary>
    [JsonPropertyName("session")]
    public string SessionId { get; set; } = string.Empty;
}
