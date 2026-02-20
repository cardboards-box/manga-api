using CardboardBox.Json;
using System.Threading.RateLimiting;
using System.Web;

namespace MangaBox.Providers;

public static class PolyfillExtensions
{
	private static IJsonService _json = new SystemTextJsonService(new JsonSerializerOptions());

	public const string USER_AGENT = Constants.USER_AGENT;

	public static readonly Dictionary<string, string> HEADERS_FOR_REFERS = new()
	{
		{"Sec-Fetch-Dest", "document"},
		{"Sec-Fetch-Mode", "navigate"},
		{"Sec-Fetch-Site", "cross-site"},
		{"Sec-Fetch-User", "?1"}
	};

	public static RateLimiter DefaultRateLimiter()  => new TokenBucketRateLimiter(new()
	{
		TokenLimit = 15,
		TokensPerPeriod = 15,
		QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
		QueueLimit = int.MaxValue,
		ReplenishmentPeriod = TimeSpan.FromSeconds(5),
		AutoReplenishment = true
	});

	public static async Task<List<T>> ToListA<T>(this IAsyncEnumerable<T> source)
	{
		var items = new List<T>();
		await foreach (var item in source)
			items.Add(item);
		return items;
	}

	//
	public static HtmlDocument ParseHtml(this string html)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(html);
		return doc;
	}
	//
	public static HtmlNode Copy(this HtmlNode node)
	{
		return node.InnerHtml.ParseHtml().DocumentNode;
	}
	//
	public static string HTMLDecode(this string text)
	{
		return HttpUtility.HtmlDecode(text).Trim('\n');
	}

	//
	public static string? InnerText(this HtmlDocument doc, string xpath)
	{
		return doc.DocumentNode.InnerText(xpath);
	}
	//
	public static string? InnerHtml(this HtmlDocument doc, string xpath)
	{
		return doc.DocumentNode.InnerHtml(xpath);
	}
	//
	public static string? Attribute(this HtmlDocument doc, string xpath, string attr)
	{
		return doc.DocumentNode.Attribute(xpath, attr);
	}
	//
	public static string? InnerText(this HtmlNode doc, string xpath)
	{

		return doc.SelectSingleNode(xpath)?.InnerText?.HTMLDecode();
	}
	//
	public static string? InnerHtml(this HtmlNode doc, string xpath)
	{
		return doc.SelectSingleNode(xpath)?.InnerHtml?.HTMLDecode();
	}
	//
	public static string? Attribute(this HtmlNode doc, string xpath, string attr)
	{
		return doc.SelectSingleNode(xpath)?.GetAttributeValue(attr, "")?.HTMLDecode();
	}

	//
	public static async Task<HtmlDocument> GetHtml(this IApiService api, string url, 
		Action<HttpRequestMessage>? config = null, CancellationToken token = default)
	{
		var json = new SystemTextJsonService(new JsonSerializerOptions());
		var req = await ((IHttpBuilder)api.Create(url, json, "GET")
			.Accept("text/html")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			}))
			.CancelWith(token))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		if (req.StatusCode == HttpStatusCode.Moved)
		{
			var location = req.Headers?.Location?.ToString();
			if (string.IsNullOrEmpty(location))
			{
				req.EnsureSuccessStatusCode();
				throw new NullReferenceException($"Request returned null for: {url}");
			}

			return await api.GetHtml(location, config, token);
		}
		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync(token);
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}

	public static async Task<HtmlDocument> PostHtml(this IApiService api, string url, 
		Action<HttpRequestMessage>? config = null, CancellationToken token = default)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "POST")
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			}))
			.CancelWith(token))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync(token);
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}

	public static async Task<HtmlDocument> PostHtml(this IApiService api, string url, (string, string)[] formData, 
		Action<HttpRequestMessage>? config = null, CancellationToken token = default)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "POST")
			.Body(formData)
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			}))
			.CancelWith(token))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync(token);
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}

	public static async Task<(Stream data, long length, string filename, string type)> GetData(this IApiService api, string url, 
		Action<HttpRequestMessage>? config = null, CancellationToken token = default)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "GET")
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			}))
			.CancelWith(token))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		var headers = req.Content.Headers;
		var path = headers?.ContentDisposition?.FileName
			?? headers?.ContentDisposition?.Parameters?.FirstOrDefault()?.Value
			?? url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Split('?', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
			?? "";
		var type = headers?.ContentType?.ToString() ?? "";
		var length = headers?.ContentLength ?? 0;

		return (await req.Content.ReadAsStreamAsync(token), length, path, type);
	}
}
