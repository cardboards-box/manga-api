namespace MangaBox.Utilities.Flare;

using Models;

/// <summary>
/// Provides helpers for building an RFC 6265 compliant Cookie request header
/// </summary>
public static class CookieHeaderBuilder
{
	/// <summary>
	/// Builds a valid HTTP <c>Cookie</c> header value for a specific request URI.
	/// </summary>
	/// <param name="cookies">The collection of cookies.</param>
	/// <param name="requestUri">The URI the request will be sent to.</param>
	/// <returns>
	/// A properly formatted Cookie header value (e.g. <c>"a=1; b=2"</c>).
	/// </returns>
	public static string BuildCookieHeader(IEnumerable<SolverCookie> cookies, Uri requestUri)
	{
		ArgumentNullException.ThrowIfNull(cookies);
		ArgumentNullException.ThrowIfNull(requestUri);

		var now = DateTimeOffset.UtcNow;
		var host = requestUri.Host;
		var requestPath = string.IsNullOrEmpty(requestUri.AbsolutePath)
			? "/"
			: requestUri.AbsolutePath;

		var isHttps = string.Equals(
			requestUri.Scheme,
			Uri.UriSchemeHttps,
			StringComparison.OrdinalIgnoreCase);

		var applicableCookies = cookies
			.Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Name))
			.Where(c => !IsExpired(c, now))
			.Where(c => !c.Secure || isHttps)
			.Where(c => DomainMatches(c.Domain, host))
			.Where(c => PathMatches(c.Path, requestPath))
			.OrderByDescending(c => (c.Path ?? "/").Length)
			.ThenBy(c => c.Name, StringComparer.Ordinal);

		return string.Join("; ",
			applicableCookies.Select(c =>
				$"{Uri.EscapeDataString(c.Name)}={Uri.EscapeDataString(c.Value ?? string.Empty)}"));
	}

	/// <summary>
	/// Determines whether a cookie has expired.
	/// </summary>
	/// <param name="cookie">The cookie to evaluate.</param>
	/// <param name="now">The current UTC time.</param>
	/// <returns>
	/// <see langword="true" /> if the cookie is expired; otherwise <see langword="false" />.
	/// </returns>
	private static bool IsExpired(SolverCookie cookie, DateTimeOffset now)
	{
		if (cookie.Expires <= 0)
			return false;

		var expiration = cookie.Expires >= 1_000_000_000_000L
			? DateTimeOffset.FromUnixTimeMilliseconds(cookie.Expires)
			: DateTimeOffset.FromUnixTimeSeconds(cookie.Expires);
		return expiration <= now;
	}

	/// <summary>
	/// Determines whether a cookie's domain matches the request host.
	/// </summary>
	/// <param name="cookieDomain">The domain defined by the cookie.</param>
	/// <param name="requestHost">The host of the outgoing request.</param>
	/// <returns>
	/// <see langword="true" /> if the cookie should be sent to the host; otherwise <see langword="false" />.
	/// </returns>
	private static bool DomainMatches(string cookieDomain, string requestHost)
	{
		if (string.IsNullOrWhiteSpace(cookieDomain))
			return true;

		cookieDomain = cookieDomain.Trim();

		var isDomainCookie = cookieDomain.StartsWith('.');
		var domain = cookieDomain.TrimStart('.');

		if (string.Equals(requestHost, domain, StringComparison.OrdinalIgnoreCase))
			return true;

		if (!isDomainCookie)
			return false;

		return requestHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether a cookie's path matches the request path.
	/// </summary>
	/// <param name="cookiePath">The path defined by the cookie.</param>
	/// <param name="requestPath">The absolute path of the outgoing request.</param>
	/// <returns>
	/// <see langword="true" /> if the cookie path matches; otherwise <see langword="false" />.
	/// </returns>
	private static bool PathMatches(string cookiePath, string requestPath)
	{
		cookiePath = string.IsNullOrWhiteSpace(cookiePath)
			? "/"
			: cookiePath;

		if (!requestPath.StartsWith(cookiePath, StringComparison.Ordinal))
			return false;

		if (requestPath.Length == cookiePath.Length)
			return true;

		if (cookiePath.EndsWith("/", StringComparison.Ordinal))
			return true;

		return requestPath[cookiePath.Length] == '/';
	}
}
