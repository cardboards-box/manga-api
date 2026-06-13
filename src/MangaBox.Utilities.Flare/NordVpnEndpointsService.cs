namespace MangaBox.Utilities.Flare;

/// <summary>
/// Gets a list of proxy_ssl endpoints from NordVPN
/// </summary>
public interface INordVpnEndpointsService
{
	/// <summary>
	/// The list of proxy_ssl endpoints from NordVPN
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The list of proxy_ssl endpoints from NordVPN</returns>
	Task<string[]> NordVpnEndpoints(CancellationToken token);
}

internal class NordVpnEndpointsService(
	ILogger<NordVpnEndpointsService> _logger) : INordVpnEndpointsService
{
	private readonly SemaphoreSlim _endpointLock = new(1, 1);
	private string[]? _endpoints;

	public async Task<string[]> NordVpnEndpoints(CancellationToken token)
	{
		if (_endpoints is not null)
			return _endpoints;

		await _endpointLock.WaitAsync(token);
		try
		{
			return _endpoints ??= await FetchEndpoints(token);
		}
		finally
		{
			_endpointLock.Release();
		}
	}

	public async Task<string[]> FetchEndpoints(CancellationToken token)
	{
		const string SERVERS_URL = "https://api.nordvpn.com/v1/servers";
		const int HTTPS_PORT = 89;

		try
		{
			using var client = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(15)
			};
			using var response = await client.GetAsync(SERVERS_URL, token);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(token);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
			var hosts = NordVpnProxyHosts(doc.RootElement);
			var endpoints = hosts
				.Select(host => $"https://{host}:{HTTPS_PORT}")
				.ToArray();

			_logger.LogInformation("Loaded {Count} NordVPN HTTP proxy endpoints.", endpoints.Length);
			return endpoints;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to load NordVPN HTTP proxy endpoints from {Url}", SERVERS_URL);
			return [];
		}
	}

	private static string[] NordVpnProxyHosts(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Array)
			return [];

		var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var server in root.EnumerateArray())
		{
			if (!JsonString(server, "status").Equals("online", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!server.TryGetProperty("technologies", out var technologies) ||
				technologies.ValueKind != JsonValueKind.Array)
				continue;

			foreach (var technology in technologies.EnumerateArray())
			{
				if (!JsonString(technology, "identifier").Equals("proxy_ssl", StringComparison.OrdinalIgnoreCase) ||
					!PivotIsOnline(technology))
					continue;

				var host = ProxyHost(technology);
				if (!string.IsNullOrWhiteSpace(host))
					hosts.Add(host);
			}
		}

		return [.. hosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
	}

	private static bool PivotIsOnline(JsonElement technology)
	{
		if (!technology.TryGetProperty("pivot", out var pivot) ||
			pivot.ValueKind != JsonValueKind.Object)
			return true;

		return JsonString(pivot, "status").Equals("online", StringComparison.OrdinalIgnoreCase);
	}

	private static string? ProxyHost(JsonElement technology)
	{
		if (!technology.TryGetProperty("metadata", out var metadata) ||
			metadata.ValueKind != JsonValueKind.Array)
			return null;

		foreach (var item in metadata.EnumerateArray())
		{
			if (JsonString(item, "name").Equals("proxy_hostname", StringComparison.OrdinalIgnoreCase))
				return JsonString(item, "value");
		}

		return null;
	}

	private static string JsonString(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) &&
			value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;
	}
}
