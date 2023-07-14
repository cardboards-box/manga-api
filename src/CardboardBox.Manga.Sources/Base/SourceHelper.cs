namespace CardboardBox.Manga.Sources;

public static partial class SourceHelper
{
    public static string IdFromUrl(string url)
    {
        return url.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
    }
}