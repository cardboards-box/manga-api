namespace MangaBox.Core.Requesting;

/// <summary>
/// Represents the result of a successful API call that returns data
/// </summary>
/// <typeparam name="T">The type of data returned</typeparam>
public class Boxed<T> : Boxed
{
    /// <summary>
    /// The result of the request
    /// </summary>
    [JsonPropertyName("data")]
    public T Data { get; set; }

    /// <summary>
    /// Represents the result of a successful API call that returns data
    /// </summary>
    /// <param name="data">The result of the request</param>
    public Boxed(T data) : base(200, DATA)
    {
        Data = data;
    }

    /// <summary>
    /// Represents the result of a successful API call that returns data
    /// </summary>
    /// <param name="data">The result of the request</param>
    /// <param name="code">The HTTP status code for the result</param>
    /// <param name="type">The type of the result</param>
    public Boxed(T data, HttpStatusCode code, string? type = null) 
        : base(code, type ?? DATA)
    {
        Data = data;
    }

    /// <summary>
    /// Represents the result of a successful API call that returns data
    /// </summary>
    /// <param name="data">The result of the request</param>
    /// <param name="code">The HTTP status code for the result</param>
    /// <param name="type">The type of the result</param>
    public Boxed(T data, int code, string? type = null) 
        : base(code, type ?? DATA)
    {
        Data = data;
    }
}
