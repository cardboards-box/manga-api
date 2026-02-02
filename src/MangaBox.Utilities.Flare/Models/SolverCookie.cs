namespace MangaBox.Utilities.Flare.Models;

/// <summary>
/// Represents a cookie for the solver
/// </summary>
/// <param name="name">The name of the cookie</param>
/// <param name="value">The value of the cookie</param>
public class SolverCookie(string name, string value)
{
    /// <summary>
    /// The name of the cookie
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = name;

    /// <summary>
    /// The value of the cookie
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = value;

    /// <summary>
    /// The domain the cookie belonged to
    /// </summary>
    [JsonPropertyName("domain")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The path to the cookie
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// When the cookie expires
    /// </summary>
    [JsonPropertyName("expiry")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Expires { get; set; } = 0;

	/// <summary>
	/// Whether or not the cookie is secure
	/// </summary>
	[JsonPropertyName("secure")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Secure { get; set; } = false;

	/// <summary>
	/// Whether or not the cookie should only be used in HTTP requests
	/// </summary>
	[JsonPropertyName("httpOnly")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HttpOnly { get; set; } = false;

    /// <summary>
    /// The same-site parameter for the cookie
    /// </summary>
    [JsonPropertyName("sameSite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string SameSite { get; set; } = string.Empty;

    [JsonConstructor]
    internal SolverCookie() : this(string.Empty, string.Empty) { }
}
