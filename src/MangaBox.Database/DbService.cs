namespace MangaBox;

using Database.Services;

public interface IDbService
{
    IChapterDbService Chapters { get; }

    IContentRatingDbService ContentRatings { get; }

    IImageDbService Images { get; }

    IPageDbService Pages { get; }

    IPersonDbService People { get; }

    IProviderDbService Providers { get; }

    ISeriesDbService Series { get; }

    ISeriesPeopleDbService SeriesPeople { get; }

    ITagDbService Tags { get; }

    IVolumeDbService Volumes { get; }

    IRequestLogDbService RequestLogs { get; }

    IProfileDbService Profiles { get; }

    IRoleDbService Roles { get; }

    ILoginDbService Logins { get; }
}

internal class DbService(
    IChapterDbService chapters,
    IContentRatingDbService contentRatings,
    IImageDbService images,
    IPageDbService pages,
    IPersonDbService people,
    IProviderDbService providers,
    ISeriesDbService series,
    ISeriesPeopleDbService seriesPeople,
    ITagDbService tags,
    IVolumeDbService volumes,
    IRequestLogDbService requestLog,
    IProfileDbService profiles,
    IRoleDbService roles,
    ILoginDbService logins) : IDbService
{
    public IChapterDbService Chapters => chapters;

    public IContentRatingDbService ContentRatings => contentRatings;

    public IImageDbService Images => images;

    public IPageDbService Pages => pages;

    public IPersonDbService People => people;

    public IProviderDbService Providers => providers;

    public ISeriesDbService Series => series;

    public ISeriesPeopleDbService SeriesPeople => seriesPeople;

    public ITagDbService Tags => tags;

    public IVolumeDbService Volumes => volumes;

    public IRequestLogDbService RequestLogs => requestLog;

    public IProfileDbService Profiles => profiles;

    public IRoleDbService Roles => roles;

    public ILoginDbService Logins => logins;
}
