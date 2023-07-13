namespace CardboardBox.Manga.Database;

using Caching;

/// <summary>
/// Joins all of the disparate database services into one interface
/// </summary>
public interface IDbService
{
    IProfileDbService Profiles { get; }

    IMangaDbService Manga { get; }

    IMangaChapterDbService Chapter { get; }

    IMangaWithChaptersDbService Chapters { get; }

    IMangaExtendedDbService Extended { get; }

    IMangaSearchDbService Search { get; }

    IMangaBookmarkDbService Bookmarks { get; }

    IMangaFavouriteDbService Favourites { get; }

    IMangaProgressDbService Progress { get; }

    ICacheDbService Cache { get; }
}

/// <summary>
/// Joins all of the disparate database services into one service
/// </summary>
public class DbService : IDbService
{
    public IProfileDbService Profiles { get; }

    public IMangaDbService Manga { get; }

    public IMangaChapterDbService Chapter { get; }

    public IMangaWithChaptersDbService Chapters { get; }

    public IMangaExtendedDbService Extended { get; }

    public IMangaSearchDbService Search { get; }

    public IMangaBookmarkDbService Bookmarks { get; }

    public IMangaFavouriteDbService Favourites { get; }

    public IMangaProgressDbService Progress { get; }

    public ICacheDbService Cache { get; }

    public DbService(
        IProfileDbService profiles, 
        IMangaDbService manga, 
        IMangaChapterDbService chapter, 
        IMangaWithChaptersDbService chapters, 
        IMangaExtendedDbService extended, 
        IMangaSearchDbService search, 
        IMangaBookmarkDbService bookmarks, 
        IMangaFavouriteDbService favourites, 
        IMangaProgressDbService progress,
        ICacheDbService cache)
    {
        Profiles = profiles;
        Manga = manga;
        Chapter = chapter;
        Chapters = chapters;
        Extended = extended;
        Search = search;
        Bookmarks = bookmarks;
        Favourites = favourites;
        Progress = progress;
        Cache = cache;
    }
}
