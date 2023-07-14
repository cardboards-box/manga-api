namespace CardboardBox.Core;

public static class MangaHelper
{
    public static string ProxyUrl(string url, string group, string? referer, bool noCache)
    {
        var path = WebUtility.UrlEncode(url);
        var uri = $"https://cba-proxy.index-0.com/proxy?path={path}&group={group}";
        if (!string.IsNullOrEmpty(referer))
            uri += $"&referer={WebUtility.UrlEncode(referer)}";
        if (noCache)
            uri += $"&noCache=true";

        return uri;
    }

    public static string ProxyUrlMangaPage(string url, string? referer = null, bool noCache = false)
    {
        return ProxyUrl(url, "manga-page", referer, noCache);
    }

    public static string ProxyUrlMangaCover(string url, string? referer = null, bool noCache = false)
    {
        return ProxyUrl(url, "manga-cover", referer, noCache);
    }

    public static string ProxyUrlExternal(string url, string? referer = null, bool noCache = false)
    {
        return ProxyUrl(url, "external", referer, noCache);
    }
}
