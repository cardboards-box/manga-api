namespace MangaBox.Providers.ThirdParty.MangaDex;

using Models;

public interface IMangaDexImporter : IMangaSource
{

}

internal class MangaDexImporter(
    IDbService _db,
    IConfiguration _config,
    IApiService _net,
    IMangaDexInteropService _api) : StandardSource(_net, _config), IMangaDexImporter
{
    public override string ProviderName => "MangaDex";

    public override string[] URLs => new[] { "mangadex.org", "mangadex.dev" };

    public override string? UserAgent => Config["MangaDex:UserAgent"];

    public static (IdParseResult result, string? id) ParseMangaId(string url)
    {
        var path = new Uri(url).AbsolutePath.Trim('/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var entity = parts.FirstOrDefault();
        if (string.IsNullOrEmpty(entity)) return (IdParseResult.Invalid, null);

        var id = parts.Skip(1).FirstOrDefault();
        if (string.IsNullOrEmpty(id)) return (IdParseResult.Invalid, null);

        if (entity.EqualsIc("chapter"))
            return (IdParseResult.Chapter, id);

        if (entity.EqualsIc("title"))
            return (IdParseResult.Manga, id);

        if (entity.EqualsIc("author"))
            return (IdParseResult.Author, id);

        if (entity.EqualsIc("group"))
            return (IdParseResult.Group, id);

        if (entity.EqualsIc("user"))
            return (IdParseResult.User, id);

        return (IdParseResult.Invalid, null);
    }

    public override async Task<Boxed> Load(string url, Provider provider)
    {
        var (type, id) = ParseMangaId(url);
        if (type == IdParseResult.Invalid || string.IsNullOrWhiteSpace(id))
            return Boxed.Bad("Invalid URL.");

        return type switch
        {
            IdParseResult.Manga => await LoadManga(provider, id),
            //IdParseResult.Chapter => await LoadChapter(provider, id),
            //IdParseResult.Author => await LoadAuthor(provider, id),
            //IdParseResult.Group => await LoadGroup(provider, id),
            //IdParseResult.User => await LoadUser(provider, id),
            _ => Boxed.Bad("Invalid URL."),
        };
    }

    public async Task<Boxed> LoadManga(Provider provider, string id)
    {
        var existing = await _db.Series.ByPlatform(provider.Id, id);
        if (existing is not null) return Boxed.Ok(existing.Id);

        var (manga, error) = await _api.GetManga(id, provider);
        if (!string.IsNullOrEmpty(error) || manga is null)
            return Boxed.NotFound("MangaDex Manga", error ?? "Manga not found.");

        return Boxed.Ok(manga.Id);
    }

    public override Task<Boxed> Update(Series series, Provider provider)
    {
        throw new NotImplementedException();
    }

    internal enum IdParseResult
    {
        Invalid = 0,
        Chapter = 1,
        Manga = 2,
        Author = 3,
        Group = 4,
        User = 5
    }
}

