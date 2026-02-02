namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// Represents a solution to a solver request
/// </summary>
public class SolverSolution
{
	/// <summary>
	/// The URL that was requested
	/// </summary>
	[JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The status code the server returned
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// The cookies from the request and response
    /// </summary>
    [JsonPropertyName("cookies")]
    public SolverCookie[] Cookies { get; set; } = [];

	/// <summary>
	/// The user-agent that was used for the request
	/// </summary>
	[JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// The headers from the response
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// The response from the solver
    /// </summary>
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
}
