using CardboardBox.Json;

namespace MangaBox.Match.SauceNao;

/// <summary>
/// A service for interacting with saucenao
/// </summary>
public interface ISauceNaoApiService
{
	/// <summary>
	/// Fetches similar images by the given URL
	/// </summary>
	/// <param name="image">The image URL</param>
	/// <param name="limit">The maximum number of results to return</param>
	/// <param name="dbs">The databases to search in</param>
	/// <returns>The results of the request</returns>
	Task<Sauce?> Get(string image, int? limit = null, SauceNaoDatabase[]? dbs = null);

	/// <summary>
	/// Fetches similar images by the given URL
	/// </summary>
	/// <param name="stream">The image stream</param>
	/// <param name="filename">The name of the image file</param>
	/// <param name="limit">The maximum number of results to return</param>
	/// <param name="dbs">The databases to search in</param>
	/// <returns>The results of the request</returns>
	Task<Sauce?> Get(Stream stream, string filename, int? limit = null, SauceNaoDatabase[]? dbs = null);
}

/// <inheritdoc cref="ISauceNaoApiService" />
internal class SauceNaoApiService(
	IApiService _api,
	IJsonService _json,
	IConfiguration _config,
	ILogger<SauceNaoApiService> _logger) : ISauceNaoApiService
{
	/// <summary>
	/// The default limit for results
	/// </summary>
	public const int DEFAULT_LIMIT = 6;

	/// <summary>
	/// The default databases to request
	/// </summary>
	public readonly SauceNaoDatabase[] DEFAULT_DBS = [];

	/// <summary>
	/// The API key for the sauce-nao API
	/// </summary>
	public string ApiKey => field ??= _config["Match:SauceToken"] ?? throw new ArgumentNullException(nameof(ApiKey));

	/// <summary>
	/// The base URL for the sauce-nao API
	/// </summary>
	public string BaseUrl => field ??= _config["Match:SauceUrl"] ?? "https://saucenao.com/search.php";

	/// <summary>
	/// Wraps the given URL and appends the given parameters
	/// </summary>
	/// <param name="parameters">The query parameters</param>
	/// <returns>The URL</returns>
	public string WrapUrl(Dictionary<string, string> parameters)
	{
		var query = string.Join("&", parameters.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
		return $"{BaseUrl}?{query}";
	}

	/// <summary>
	/// Gets the default parameters for a request
	/// </summary>
	/// <param name="limit">The number of results to limit to</param>
	/// <param name="dbs">The databases to get results from</param>
	/// <returns>The parameters</returns>
	public Dictionary<string, string> DefaultParameters(int? limit, SauceNaoDatabase[]? dbs)
	{
		var pars = new Dictionary<string, string>
		{
			["output_type"] = "2",
			["api_key"] = ApiKey,
			["numres"] = (limit ?? DEFAULT_LIMIT).ToString(),
		};

		if (dbs == null || dbs.Length == 0) 
			pars.Add("db", "999");

		foreach (var db in dbs ?? DEFAULT_DBS)
			pars.Add("dbs[]", ((int)db).ToString());

		return pars;
	}

	/// <inheritdoc />
	public async Task<Sauce?> Get(string image, int? limit, SauceNaoDatabase[]? dbs)
	{
		try
		{
			var pars = DefaultParameters(limit, dbs);
			pars["url"] = image;
			var url = WrapUrl(pars);
			return await _api.Get<Sauce>(url);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching sauce-nao results for image {Image}", image);
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<Sauce?> Get(Stream stream, string filename, int? limit, SauceNaoDatabase[]? dbs)
	{
		try
		{
			var pars = DefaultParameters(limit, dbs);
			var url = WrapUrl(pars);

			using var content = new StreamContent(stream);
			using var body = new MultipartFormDataContent
			{
				{ content, "file", filename }
			};

			var result = await ((IHttpBuilder)_api
				.Create(url, _json, "POST")
				.BodyContent(body))
				.Result();

			if (result is null || !result.IsSuccessStatusCode)
			{
				string? badContent = null;
				if (result is not null)
					badContent = await result.Content.ReadAsStringAsync();

				_logger.LogError("Error occurred while processing saucenao response: {code} - {content}", result?.StatusCode, badContent);
				return null;
			}

			using var resStream = await result.Content.ReadAsStreamAsync();
			var output = await _json.Deserialize<Sauce>(resStream);
			return output;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching sauce-nao results for image stream {Filename}", filename);
			return null;
		}
	}
}
