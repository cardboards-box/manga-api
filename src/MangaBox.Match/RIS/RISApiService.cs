using CardboardBox.Json;

namespace MangaBox.Match.RIS;

/// <summary>
/// A service for interacting with the Match API
/// </summary>
public interface IRISApiService
{
	/// <summary>
	/// Add an image by URL
	/// </summary>
	/// <typeparam name="T">The type of metadata to include</typeparam>
	/// <param name="url">The URL of the image</param>
	/// <param name="id">The unique ID of the image</param>
	/// <param name="json">The metadata value to add</param>
	/// <returns>The results of the request</returns>
	Task<RISResult> Add<T>(string url, string id, T json);

	/// <summary>
	/// Add an image by URL
	/// </summary>
	/// <param name="url">The URL of the image</param>
	/// <param name="id">The unique ID of the image</param>
	/// <param name="json">The metadata value to add</param>
	/// <returns>The results of the request</returns>
	Task<RISResult> Add(string url, string id, string? json = null);

	/// <summary>
	/// Adds an image by stream
	/// </summary>
	/// <typeparam name="T">The type of metadata to include</typeparam>
	/// <param name="io">The image stream</param>
	/// <param name="fileName">The name of the image file</param>
	/// <param name="id">The unique ID of the image</param>
	/// <param name="json">The metadata value to add</param>
	/// <returns>The results of the request</returns>
	Task<RISResult> Add<T>(Stream io, string fileName, string id, T json);

	/// <summary>
	/// Adds an image by stream
	/// </summary>
	/// <param name="io">The image stream</param>
	/// <param name="fileName">The name of the image file</param>
	/// <param name="id">The unique ID of the image</param>
	/// <param name="json">The metadata value to add</param>
	/// <returns>The results of the request</returns>
	Task<RISResult> Add(Stream io, string fileName, string id, string? json = null);

	/// <summary>
	/// Delete an image by it's unique ID
	/// </summary>
	/// <param name="id">The unique ID of the image</param>
	/// <returns>The result of the request</returns>
	Task<RISResult> Delete(string id);

	/// <summary>
	/// Searches for a matching image
	/// </summary>
	/// <param name="url">The URL of the image</param>
	/// <param name="allOris">Whether to search all orientations</param>
	/// <returns>The results of the search</returns>
	Task<RISSearchResult> Search(string url, bool allOris = false);

	/// <summary>
	/// Searches for a matching image
	/// </summary>
	/// <typeparam name="T">The type of meta-data included in the results</typeparam>
	/// <param name="url">The URL of the image</param>
	/// <param name="allOris">Whether to search all orientations</param>
	/// <returns>The results of the search</returns>
	Task<RISSearchResult<T>> Search<T>(string url, bool allOris = false);

	/// <summary>
	/// Searches for a matching image
	/// </summary>
	/// <typeparam name="T">The type of meta-data included in the results</typeparam>
	/// <param name="io">The image stream</param>
	/// <param name="filename">The name of the image file</param>
	/// <param name="allOris">Whether to search all orientations</param>
	/// <returns>The results of the search</returns>
	Task<RISSearchResult<T>> Search<T>(Stream io, string filename, bool allOris = false);

	/// <summary>
	/// Compares the two given images
	/// </summary>
	/// <param name="a">The first image URL</param>
	/// <param name="b">The second image URL</param>
	/// <returns>The result of the comparison</returns>
	Task<RISCompareResult> Compare(string a, string b);

	/// <summary>
	/// Fetches the number of images in the database
	/// </summary>
	/// <returns>The results of the request</returns>
	Task<RISResult<int>> Count();

	/// <summary>
	/// Pages through the images in the database
	/// </summary>
	/// <param name="offset">The number of images to skip</param>
	/// <param name="limit">The maximum number of images to return</param>
	/// <returns>The results of the request</returns>
	Task<RISResult<string>> List(int offset = 0, int limit = 20);

	/// <summary>
	/// Pings the database to see if it's live
	/// </summary>
	/// <returns>The results of the request</returns>
	Task<RISResult> Ping();
}

/// <inheritdoc cref="IRISApiService" />
internal class RISApiService(
	IApiService _api,
	IJsonService _json,
	IConfiguration _config,
	ILogger<RISApiService> _logger) : IRISApiService
{
	/// <summary>
	/// The URL to the match API service
	/// </summary>
	public string MatchUrl => field ??= _config["Match:Url"]?.TrimEnd('/').ForceNull() ?? throw new ArgumentNullException("Match:Url");

	/// <summary>
	/// Makes a request to the underlying API
	/// </summary>
	/// <typeparam name="T">The type of the result</typeparam>
	/// <param name="url">The URL to request</param>
	/// <param name="method">The HTTP method to use</param>
	/// <param name="parameters">The body parameters</param>
	/// <returns>The result of the request</returns>
	public async Task<T> Request<T>(string url, string method, params (string key, string value)[] parameters)
		where T : RISResult
	{
		try
		{
			var req = _api.Create($"{MatchUrl}/{url.TrimStart('/')}", _json, method);

			if (parameters != null && parameters.Length > 0)
				req.Body(parameters);

			return await req.Result<T>()
				?? throw new NullReferenceException("Received null response from Match API");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error making request to Match API: {Method} {Url}", method, url);
			var res = Activator.CreateInstance<T>();
			res.Status = "error";
			res.Error = [ex.Message];
			res.Method = method;
			return res;
		}
	}

	/// <summary>
	/// Makes a request with configuration multipart form data
	/// </summary>
	/// <typeparam name="T">The type of the result</typeparam>
	/// <param name="url">The URL to request</param>
	/// <param name="method">The HTTP method to use</param>
	/// <param name="config">The configuration option for the content</param>
	/// <param name="parameters">The body parameters</param>
	/// <returns>The result of the request</returns>
	public async Task<T> Request<T>(string url, string method, 
		Action<MultipartFormDataContent> config, params (string key, string value)[] parameters)
		where T : RISResult
	{
		try
		{
			var req = _api.Create($"{MatchUrl}/{url.TrimStart('/')}", _json, method);
			using var body = new MultipartFormDataContent();
			foreach(var (key, value) in parameters)
				body.Add(new StringContent(value), key);
			config?.Invoke(body);
			req.BodyContent(body);
			return await req.Result<T>()
				?? throw new NullReferenceException("Received null response from Match API");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error making request to Match API: {Method} {Url}", method, url);
			var res = Activator.CreateInstance<T>();
			res.Status = "error";
			res.Error = [ex.Message];
			res.Method = method;
			return res;
		}
	}

	/// <summary>
	/// Converts the given stream to a memory stream
	/// </summary>
	/// <param name="stream">The stream</param>
	/// <returns>The memory stream</returns>
	public static async Task<MemoryStream> ToMemoryStream(Stream stream)
	{
		if (stream is MemoryStream ms)
			return ms;

		ms = new MemoryStream();
		await stream.CopyToAsync(ms);
		ms.Position = 0;
		return ms;
	}

	/// <inheritdoc />
	public Task<RISResult> Add<T>(string url, string id, T json)
	{
		var meta = _json.Serialize(json);
		return Add(url, id, meta);
	}

	/// <inheritdoc />
	public Task<RISResult> Add(string url, string id, string? json = null)
	{
		var pars = new List<(string, string)>
		{
			("url", url), ("filepath", id)
		};

		if (!string.IsNullOrEmpty(json))
			pars.Add(("metadata", json));

		return Request<RISResult>("add", "POST", [.. pars]);
	}

	/// <inheritdoc />
	public Task<RISResult> Add<T>(Stream io, string fileName, string id, T json)
	{
		var meta = _json.Serialize(json);
		return Add(io, fileName, meta);
	}

	/// <inheritdoc />
	public async Task<RISResult> Add(Stream io, string fileName, string id, string? json = null)
	{
		var pars = new List<(string, string)>
		{
			("filepath", id)
		};

		if (!string.IsNullOrEmpty(json))
			pars.Add(("metadata", json));

		var ms = await ToMemoryStream(io);
		using var byteContent = new ByteArrayContent(ms.ToArray());
		return await Request<RISResult>("add", "POST",
			c => c.Add(byteContent, "image", fileName),
			[..pars]);
	}

	/// <inheritdoc />
	public Task<RISCompareResult> Compare(string a, string b)
	{
		return Request<RISCompareResult>("compare", "POST", ("url1", a), ("url2", b));
	}

	/// <inheritdoc />
	public Task<RISResult<int>> Count()
	{
		return Request<RISResult<int>>("count", "GET");
	}

	/// <inheritdoc />
	public Task<RISResult> Delete(string id)
	{
		return Request<RISResult>("delete", "DELETE", ("filepath", id));
	}

	/// <inheritdoc />
	public Task<RISResult<string>> List(int offset = 0, int limit = 20)
	{
		return Request<RISResult<string>>("list", "GET", 
			("offset", offset.ToString()), 
			("limit", limit.ToString()));
	}

	/// <inheritdoc />
	public Task<RISResult> Ping()
	{
		return Request<RISResult>("ping", "GET");
	}

	/// <inheritdoc />
	public Task<RISSearchResult> Search(string url, bool allOris = false)
	{
		return Request<RISSearchResult>("search", "POST", 
			("url", url), 
			("all_orientations", allOris ? "true" : "false"));
	}

	/// <inheritdoc />
	public Task<RISSearchResult<T>> Search<T>(string url, bool allOris = false)
	{
		return Request<RISSearchResult<T>>("search", "POST",
			("url", url),
			("all_orientations", allOris ? "true" : "false"));
	}

	/// <inheritdoc />
	public async Task<RISSearchResult<T>> Search<T>(Stream io, string fileName, bool allOris = false)
	{
		var ms = await ToMemoryStream(io);
		using var byteContent = new ByteArrayContent(ms.ToArray());
		return await Request<RISSearchResult<T>>("search", "POST",
			c => c.Add(byteContent, "image", fileName),
			("all_orientations", allOris ? "true" : "false"));
	}
}
