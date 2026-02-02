namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// A response from the solver
/// </summary>
public class SolverResponse : SolverBaseResponse
{
    /// <summary>
    /// The solution to the request
    /// </summary>
    [JsonPropertyName("solution")]
    public SolverSolution Solution { get; set; } = new();
}