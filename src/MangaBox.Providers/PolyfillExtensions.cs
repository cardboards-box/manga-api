using CardboardBox.Json;
using HtmlAgilityPack;
using System.Web;

namespace MangaBox.Providers;

public static class PolyfillExtensions
{
	private static IJsonService _json = new SystemTextJsonService(new JsonSerializerOptions());

	public const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";

	public static string Join(this IEnumerable<HtmlNode> nodes, bool checkWs = false)
	{
		try
		{
			var doc = new HtmlDocument();
			foreach (var node in nodes)
				if (!checkWs || !string.IsNullOrWhiteSpace(node.InnerText))
					doc.DocumentNode.AppendChild(node);

			return doc.DocumentNode?.InnerHtml?.Trim() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	public static void CleanupNode(this HtmlNode parent)
	{
		parent.SelectNodes("./noscript")?
			.ToList()
			.ForEach(t => t.Remove());

		parent.SelectNodes("./img")?
			.ToList()
			.ForEach(t =>
			{
				foreach (var attr in t.Attributes.ToArray())
					if (attr.Name != "src" && attr.Name != "alt")
						t.Attributes.Remove(attr);
			});

		if (parent.ChildNodes.Count == 0)
		{
			parent.Remove();
			return;
		}

		if (parent.ParentNode != null &&
			parent.ChildNodes.Count == 1 &&
			parent.FirstChild.Name == "img")
		{
			parent.ParentNode.InsertBefore(parent.FirstChild, parent);
			parent.Remove();
			return;
		}
	}

	public static async IAsyncEnumerable<T> WhereA<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
	{
		await foreach (var item in source)
		{
			if (predicate(item))
				yield return item;
		}
	}

	public static async IAsyncEnumerable<T> SkipWhileA<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
	{
		var skipping = true;
		await foreach (var item in source)
		{
			if (skipping && predicate(item))
				continue;
			skipping = false;
			yield return item;
		}
	}

	public static async Task<T[]> ToArrayA<T>(this IAsyncEnumerable<T> source)
	{
		return [..await ToListA(source)];
	}

	public static async Task<List<T>> ToListA<T>(this IAsyncEnumerable<T> source)
	{
		var items = new List<T>();
		await foreach (var item in source)
			items.Add(item);
		return items;
	}

	public static async Task<T?> FirstOrDefaultA<T>(this IAsyncEnumerable<T> source)
	{
		await foreach (var item in source)
			return item;

		return default;
	}


	//
	public static async Task<HtmlDocument?> GetHtml(this IHttpBuilder builder)
	{
		using var resp = await builder.Result();
		if (resp == null || !resp.IsSuccessStatusCode) return null;

		var data = await resp.Content.ReadAsStringAsync();
		return data.ParseHtml();
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

	public static string SafeSubString(this string text, int length, int start = 0)
	{
		if (start + length > text.Length)
			return text[start..];

		return text.Substring(start, length);
	}
	//
	public static string GetRootUrl(this string url)
	{
		if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
			throw new UriFormatException(url);

		return uri.GetRootUrl();
	}
	//
	public static string GetRootUrl(this Uri uri)
	{
		var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
		return $"{uri.Scheme}://{uri.Host}{port}";
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
	public static async Task<HtmlDocument> GetHtml(this IApiService api, string url, Action<HttpRequestMessage>? config = null)
	{
		var json = new SystemTextJsonService(new JsonSerializerOptions());
		var req = await ((IHttpBuilder)api.Create(url, json, "GET")
			.Accept("text/html")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			})))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		if (req.StatusCode == HttpStatusCode.Moved)
		{
			var location = req.Headers?.Location?.ToString();
			if (string.IsNullOrEmpty(location))
			{
				req.EnsureSuccessStatusCode();
				throw new NullReferenceException($"Request returned null for: {url}");
			}

			return await api.GetHtml(location, config);
		}
		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync();
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}

	public static async Task<HtmlDocument> PostHtml(this IApiService api, string url, Action<HttpRequestMessage>? config = null)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "POST")
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			})))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync();
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}

	public static async Task<HtmlDocument> PostHtml(this IApiService api, string url, (string, string)[] formData, Action<HttpRequestMessage>? config = null)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "POST")
			.Body(formData)
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			})))
			.Result() ?? throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		using var io = await req.Content.ReadAsStreamAsync();
		var doc = new HtmlDocument();
		doc.Load(io);

		return doc;
	}
	//
	public static async Task<(Stream data, long length, string filename, string type)> GetData(this IApiService api, string url, Action<HttpRequestMessage>? config = null)
	{
		var req = await ((IHttpBuilder)api.Create(url, _json, "GET")
			.Accept("*/*")
			.With(c => c.Message(c =>
			{
				c.Headers.Add("user-agent", USER_AGENT);
				config?.Invoke(c);
			})))
			.Result();
		if (req == null)
			throw new NullReferenceException($"Request returned null for: {url}");

		req.EnsureSuccessStatusCode();

		var headers = req.Content.Headers;
		var path = headers?.ContentDisposition?.FileName
			?? headers?.ContentDisposition?.Parameters?.FirstOrDefault()?.Value
			?? url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Split('?', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
			?? "";
		var type = headers?.ContentType?.ToString() ?? "";
		var length = headers?.ContentLength ?? 0;

		return (await req.Content.ReadAsStreamAsync(), length, path, type);
	}
}
