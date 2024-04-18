namespace MangaBox.Core.Requesting;

/// <summary>
/// Represents the base return result for all API calls
/// </summary>
public class Boxed
{
    /// <summary>
    /// Result was successful and contained no data
    /// </summary>
    public const string OK = "ok";

    /// <summary>
    /// Result was an error
    /// </summary>
    public const string ERROR = "error";

    /// <summary>
    /// Result was successful and contained data
    /// </summary>
    public const string DATA = "data";

    /// <summary>
    /// Result was successful and contained a collection of data
    /// </summary>
    public const string ARRAY = "array";

    /// <summary>
    /// Result was successful and contained a paged collection of data
    /// </summary>
    public const string PAGED = "paged";

    /// <summary>
    /// Request ID (useful for debugging)
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// The boxed code for the result
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// The HTTP status code for the result
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Represents the base return result for all API calls
    /// </summary>
    /// <param name="code">The HTTP status code result</param>
    /// <param name="type">The type of result</param>
    public Boxed(int code, string? type = null)
    {
        Code = code;

        if (!string.IsNullOrWhiteSpace(type))
        {
            Type = type;
            return;
        }

        Type = code >= 200 && code < 300 ? OK : ERROR;
    }

    /// <summary>
    /// Represents the base return result for all API calls
    /// </summary>
    /// <param name="code">The HTTP status code</param>
    /// <param name="type">The type of result</param>
    public Boxed(HttpStatusCode code, string? type = null) : this((int)code, type) { }

    /// <summary>
    /// Result was successful and contained no data
    /// </summary>
    /// <returns>The returned result</returns>
    public static Boxed Ok() => new(HttpStatusCode.OK);

    /// <summary>
    /// Result was successful and contained data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static Boxed<T> Ok<T>(T data) => new(data);

    /// <summary>
    /// Result was successful and contained a collection of data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedArray<T> Ok<T>(params T[] data) => new(data);

    /// <summary>
    /// Result was successful and contained a collection of data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedArray<T> Ok<T>(IEnumerable<T> data) => new(data.ToArray());

    /// <summary>
    /// Result was successful and contained a collection of data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedArray<T> Ok<T>(List<T> data) => new([.. data]);

    /// <summary>
    /// Result was successful and contained a collection of paged data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedPaged<T> Ok<T>(PaginatedResult<T> data) => new(data);

    /// <summary>
    /// Result was successful and contained a collection of paged data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="pages">The number of pages in the result</param>
    /// <param name="total">The total number of results</param>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedPaged<T> Ok<T>(int pages, int total, params T[] data)
    {
        return new BoxedPaged<T>(data, pages, total);
    }

    /// <summary>
    /// Result was successful and contained a collection of paged data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="pages">The number of pages in the result</param>
    /// <param name="total">The total number of results</param>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedPaged<T> Ok<T>(int pages, int total, IEnumerable<T> data)
    {
        return new BoxedPaged<T>(data.ToArray(), pages, total);
    }

    /// <summary>
    /// Result was successful and contained a collection of paged data
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="pages">The number of pages in the result</param>
    /// <param name="total">The total number of results</param>
    /// <param name="data">The result data</param>
    /// <returns>The returned result</returns>
    public static BoxedPaged<T> Ok<T>(int pages, int total, List<T> data)
    {
        return new BoxedPaged<T>([.. data], pages, total);
    }

    /// <summary>
    /// An exception occurred
    /// </summary>
    /// <param name="errors">The error(s) that occurred</param>
    /// <returns>The returned error result</returns>
    public static BoxedError Exception(params string[] errors)
    {
        return new BoxedError("500 - An error occurred", errors);
    }

    /// <summary>
    /// An exception occurred
    /// </summary>
    /// <param name="exceptions">The error(s) that occurred</param>
    /// <returns>The returned error result</returns>
    public static BoxedError Exception(params Exception[] exceptions)
    {
        return new BoxedError("500 - An error occurred", exceptions.Select(e => e.Message).ToArray());
    }

    /// <summary>
    /// Something is missing
    /// </summary>
    /// <param name="resource">The resource that was missing</param>
    /// <returns>The returned error result</returns>
    public static BoxedError NotFound(string resource)
    {
        return new BoxedError(HttpStatusCode.NotFound, "404 - Something is missing", $"The requested resource '{resource}' was not found");
    }

    /// <summary>
    /// User is unauthorized
    /// </summary>
    /// <param name="issues">Any issues that occurred that caused the unauthorized error</param>
    /// <returns>The returned error result</returns>
    public static BoxedError Unauthorized(params string[] issues)
    {
        return new BoxedError(HttpStatusCode.Unauthorized, "401 - Unauthorized", ["You are not authorized to access this resource", ..issues]);
    }

    /// <summary>
    /// Something the user did was bad
    /// </summary>
    /// <param name="errors">The different errors</param>
    /// <returns>The returned error result</returns>
    public static BoxedError Bad(params string[] errors)
    {
        return new BoxedError(HttpStatusCode.BadRequest, "400 - User input is bad", errors);
    }

    /// <summary>
    /// Something the user did was bad
    /// </summary>
    /// <param name="errors">The different errors</param>
    /// <returns>The returned error result</returns>
    public static BoxedError Bad(IEnumerable<string> errors) => Bad(errors.ToArray());

    /// <summary>
    /// Resource already exists
    /// </summary>
    /// <param name="resource">The resource that was conflicting</param>
    /// <returns>The result of the request</returns>
    public static BoxedError Conflict(string resource)
    {
        return new BoxedError(HttpStatusCode.Conflict, "409 - Already exists", $"The resource '{resource}' already exists");
    }
}
