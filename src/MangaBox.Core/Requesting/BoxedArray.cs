namespace MangaBox.Core.Requesting;

/// <summary>
/// The result of a successful API call that returns a collection of data
/// </summary>
/// <typeparam name="T">The type of data</typeparam>
/// <param name="data">The data that was returned</param>
/// <param name="code">The status code of the result</param>
/// <param name="type">The type of the result</param>
public class BoxedArray<T>(T[] data, HttpStatusCode? code = null, string? type = null) 
    : Boxed<T[]>(data, code ?? HttpStatusCode.OK, type ?? ARRAY)
{
}
