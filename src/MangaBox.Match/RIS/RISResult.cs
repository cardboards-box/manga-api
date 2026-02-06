namespace MangaBox.Match.RIS;

/// <summary>
/// The base results that come back from the reverse image search API
/// </summary>
public class RISResult
{
	/// <summary>
	/// The status of the result
	/// </summary>
	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	/// <summary>
	/// Any errors that occurred while searching
	/// </summary>
	[JsonPropertyName("error")]
	public string[] Error { get; set; } = [];

	/// <summary>
	/// The method of the request
	/// </summary>
	[JsonPropertyName("method")]
	public string Method { get; set; } = string.Empty;

	/// <summary>
	/// Whether or not the request was successful
	/// </summary>
	[JsonIgnore]
	public bool Success => Status.EqualsIc("ok");
}

/// <summary>
/// The results that come back from the reverse image search API that contains data
/// </summary>
/// <typeparam name="T"></typeparam>
public class RISResult<T> : RISResult
{
	/// <summary>
	/// The results of the request
	/// </summary>
	[JsonPropertyName("result")]
	public T[] Result { get; set; } = [];
}