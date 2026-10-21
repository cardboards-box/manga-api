namespace MangaBox.Providers.Sources;

using Models.Types;

/// <summary>Reads WeebCentral series, complete chapter lists, and reader images.</summary>
public interface IWeebCentralSource : IMangaUrlSource { }

/// <summary>
/// Loads the public HTML fragments used by WeebCentral's reader. Chapter lists
/// must come from the full-list endpoint because the series page is abbreviated.
/// Images are downloaded through the application's configured proxy pool.
/// </summary>
/// <param name="_api">The shared HTTP request service.</param>
internal class WeebCentralSource(IApiService _api) : BaseMangaSource<WeebCentralSource>, IWeebCentralSource
{
    /// <inheritdoc />
    public override string HomeUrl => "https://weebcentral.com";
    /// <inheritdoc />
    public override string Provider => "weebcentral";
    /// <inheritdoc />
    public override string Name => "WeebCentral";
    /// <inheritdoc />
    public override bool UseProxiedImages => true;

    /// <summary>Checks the ULID identifiers used by WeebCentral.</summary>
    private static bool ValidId(string id) => Regex.IsMatch(id, "\\A[0-9A-HJKMNP-TV-Z]{26}\\z");

    /// <summary>Extracts a series or chapter identifier from an exact WeebCentral host.</summary>
    private static string? UrlId(string url, string resource)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "weebcentral.com" ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0] == resource && ValidId(parts[1]) ? parts[1] : null;
    }

    /// <inheritdoc />
    public override (bool matches, string? part) MatchesProvider(string url)
    {
        var id = UrlId(url, "series");
        return (id is not null, id);
    }

    /// <summary>Downloads an HTML document through the shared API service.</summary>
    private async Task<HtmlDocument> Html(string url, CancellationToken token)
    {
        using var response = await _api.Get(url, request => request
            .UserAgent(UserAgent ?? string.Empty)
            .Message(message => message.Headers.Referrer = new Uri(HomeUrl + "/")), token);
        if (response is null) throw new HttpRequestException($"No WeebCentral response for {url}.");
        response.EnsureSuccessStatusCode();
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync(token));
        return document;
    }

    /// <inheritdoc />
    public override async Task<ImportManga?> Manga(string id, CancellationToken token)
    {
        if (!ValidId(id)) return null;
        var document = await Html($"{HomeUrl}/series/{id}", token);
        var chapters = await Html($"{HomeUrl}/series/{id}/full-chapter-list", token);
        return ParseManga(id, document, chapters);
    }

    /// <summary>Decodes text and normalizes surrounding whitespace without changing names.</summary>
    private static string Text(HtmlNode? node) => HtmlEntity.DeEntitize(node?.InnerText ?? string.Empty).Trim();

    /// <summary>Finds a metadata list item by its visible label.</summary>
    private static HtmlNode? Metadata(HtmlDocument document, string label) =>
        document.DocumentNode.SelectNodes("//li[strong]")?.FirstOrDefault(node =>
            Text(node.SelectSingleNode("./strong")).TrimEnd(':') == label);

    /// <summary>Reads linked values belonging to a metadata label.</summary>
    private static string[] Links(HtmlDocument document, string label) =>
        Metadata(document, label)?.SelectNodes(".//a")?.Select(Text).Where(x => x.Length > 0).Distinct().ToArray() ?? [];

    /// <summary>Converts series metadata and the complete chapter fragment into an import.</summary>
    internal ImportManga ParseManga(string id, HtmlDocument document, HtmlDocument chapterList)
    {
        var title = Text(document.DocumentNode.SelectSingleNode("//h1"));
        var cover = document.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", "");
        var chapters = ParseChapters(chapterList);
        if (title.Length == 0 || string.IsNullOrWhiteSpace(cover) || chapters.Count == 0)
            throw new InvalidDataException("WeebCentral returned incomplete series metadata or no chapters.");

        var attributes = new List<ImportAttribute>();
        foreach (var label in new[] { "Type", "Status", "Released", "Official Translation", "Anime Adaptation", "Adult Content" })
        {
            var node = Metadata(document, label);
            if (node is null) continue;
            var clone = node.CloneNode(true);
            clone.SelectSingleNode("./strong")?.Remove();
            attributes.Add(new(label, Text(clone)));
        }
        var adult = attributes.Any(a => a.Name == "Adult Content" && a.Value.EqualsIc("Yes"));
        return new()
        {
            Id = id, Provider = Provider, Title = title,
            HomePage = $"{HomeUrl}/series/{id}",
            Cover = [new Uri(new Uri(HomeUrl), cover).AbsoluteUri],
            Description = Text(Metadata(document, "Description")?.SelectSingleNode("./p")),
            Authors = Links(document, "Author(s)"), Tags = Links(document, "Tags(s)"),
            AltTitles = Metadata(document, "Associated Name(s)")?.SelectNodes("./ul/li")?.Select(Text).ToArray() ?? [],
            Attributes = attributes, Chapters = chapters, Referer = Referer,
            Nsfw = adult, Rating = adult ? ContentRating.Erotica : ContentRating.Safe,
            OrdinalVolumeReset = false
        };
    }

    /// <summary>Parses numeric and decimal chapters, retaining stable provider chapter IDs.</summary>
    private List<ImportChapter> ParseChapters(HtmlDocument document)
    {
        var chapters = new List<ImportChapter>();
        foreach (var anchor in document.DocumentNode.SelectNodes("//a[contains(@href,'/chapters/')]")?.AsEnumerable() ?? [])
        {
            var url = new Uri(new Uri(HomeUrl), anchor.GetAttributeValue("href", "")).AbsoluteUri;
            var id = UrlId(url, "chapters");
            if (id is null) continue;
            var title = Text(anchor.SelectSingleNode("./span[contains(@class,'grow')]/span[1]"));
            if (title.Length == 0) title = Text(anchor);
            var match = Regex.Match(title, @"\AChapter\s+(\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidDataException($"Unrecognized WeebCentral chapter: {title}");
            var date = anchor.SelectSingleNode(".//time")?.GetAttributeValue("datetime", "");
            chapters.Add(new()
            {
                Id = id, Url = url, Title = title,
                Number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                Attributes = string.IsNullOrEmpty(date) ? [] : [new("Release Date", date)]
            });
        }
        return chapters.DistinctBy(c => c.Id).OrderBy(c => c.Number).ToList();
    }

    /// <inheritdoc />
    public override Task<ImportPage[]> ChapterPages(string mangaId, string chapterId, CancellationToken token) =>
        ValidId(chapterId) ? ChapterPages($"{HomeUrl}/chapters/{chapterId}", token) : Task.FromResult<ImportPage[]>([]);

    /// <inheritdoc />
    public async Task<ImportPage[]> ChapterPages(string url, CancellationToken token)
    {
        var id = UrlId(url, "chapters");
        if (id is null) return [];
        var document = await Html($"{HomeUrl}/chapters/{id}/images?is_prev=False&current_page=1&reading_style=long_strip", token);
        return ParsePages(document);
    }

    /// <summary>Reads only reader images, preserving the server's page order.</summary>
    internal ImportPage[] ParsePages(HtmlDocument document) =>
        document.DocumentNode.SelectNodes("//img[starts-with(@alt,'Page ')]")?
            .Select(image => new ImportPage(new Uri(new Uri(HomeUrl), image.GetAttributeValue("src", "")).AbsoluteUri))
            .ToArray() ?? [];
}
