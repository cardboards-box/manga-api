namespace MangaBox.Core.Requesting;

/// <summary>
/// The result of a failed API call
/// </summary>
/// <param name="code">The status code of the result</param>
/// <param name="description">A brief description of the error</param>
/// <param name="errors">Any issues that occurred</param>
public class BoxedError(HttpStatusCode code, string description, params string[] errors) 
    : Boxed<string[]>(errors, code, ERROR)
{
    /// <summary>
    /// A brief description of the error
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = description;

    /// <summary>
    /// The result of a failed API call (HTTP 500)
    /// </summary>
    /// <param name="description">A brief description of the error</param>
    /// <param name="errors">Any issues that occurred</param>
    public BoxedError(string description, params string[] errors) 
        : this(HttpStatusCode.InternalServerError, description, errors) { }

    /// <summary>
    /// The result of a failed API call (HTTP 500)
    /// </summary>
    /// <param name="code">The status code of the result</param>
    /// <param name="description">A brief description of the error</param>
    /// <param name="errors">Any issues that occurred</param>
    public BoxedError(int code, string description, params string[] errors) 
        : this((HttpStatusCode)code, description, errors) { }
}
