namespace MangaBox;

using Database.Handlers;
using Database.Services;
using Models;

public static class DiExtensions
{
    public static IDependencyResolver AddDatabase(this IDependencyResolver resolver)
    {
        return resolver
            .Add<IChapterDbService, ChapterDbService, Chapter>()
            .Add<IContentRatingDbService, ContentRatingDbService, ContentRating>()
            .Add<IImageDbService, ImageDbService, Image>()
            .Add<IPageDbService, PageDbService, Page>()
            .Add<IPersonDbService, PersonDbService, Person>()
            .Add<IProviderDbService, ProviderDbService, Provider>()
            .Add<ISeriesDbService, SeriesDbService, Series>()
            .Add<ISeriesPeopleDbService, SeriesPeopleDbService, SeriesPeople>()
            .Add<ITagDbService, TagDbService, Tag>()
            .Add<IVolumeDbService, VolumeDbService, Volume>()
            .Add<IRequestLogDbService, RequestLogDbService, RequestLog>()
            .Add<IProfileDbService, ProfileDbService, Profile>()
            .Add<ILoginDbService, LoginDbService, Login>()
            .Add<IRoleDbService, RoleDbService, Role>()

            .Transient<IDbService, DbService>()

            .Type<ExternalLink>("mb_external_link")

            .Mapping(c => c
                .Enum<ImageType>()
                .Enum<PageType>()
                .Enum<PersonRelationship>()
                .Enum<SeriesStatus>()
                .Enum<SourceType>()
                .Enum<TagType>());
    }

    private static IDependencyResolver Add<TInterface, TConcrete, TModel>(this IDependencyResolver resolver)
        where TInterface : class, IOrmMap<TModel>
        where TConcrete : class, TInterface
        where TModel : DbObject
    {
        return resolver
            .Model<TModel>()
            .Transient<TInterface, TConcrete>();
    }

    private static ITypeMapBuilder Enum<T>(this ITypeMapBuilder builder)
        where T : struct, Enum
    {
        return builder.TypeHandler<T, EnumHandler<T>>();
    }
}
