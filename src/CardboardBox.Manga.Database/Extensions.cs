namespace CardboardBox.Manga.Database;

using Base;
using Caching;

public static class Extensions
{
    public static IDependencyBuilder AddDatabase(this IDependencyBuilder builder)
    {
        //Register database models
        builder
            //Register type models
            .Model<DbMangaAttribute>()
            .Model<DbMangaChapterProgress>()
            //Register table models
            .Model<DbProfile>()
            .Model<DbManga>()
            .Model<DbMangaCache>()
            .Model<DbMangaBookmark>()
            .Model<DbMangaChapter>()
            .Model<DbMangaChapterCache>()
            .Model<DbMangaFavourite>()
            .Model<DbMangaProgress>()
            //Register mapped composites
            .Model<MangaStats>()
            .Model<GraphOut>()
            .Model<DbFilter>();

        builder
            .Transient<IFakeUpsertQueryService, FakeUpsertQueryService>()
            //Register all database services
            .Transient<IProfileDbService, ProfileDbService>()
            //Register all manga related database services
            .Transient<IMangaDbService, MangaDbService>()
            .Transient<IMangaChapterDbService, MangaChapterDbService>()
            .Transient<IMangaWithChaptersDbService, MangaWithChaptersDbService>()
            .Transient<IMangaExtendedDbService, MangaExtendedDbService>()
            .Transient<IMangaSearchDbService, MangaSearchDbService>()
            .Transient<IMangaBookmarkDbService, MangaBookmarkDbService>()
            .Transient<IMangaFavouriteDbService, MangaFavouriteDbService>()
            .Transient<IMangaProgressDbService, MangaProgressDbService>()
            //Register all manga cache related database services
            .Transient<IMangaCacheDbService, MangaCacheDbService>()
            .Transient<IMangaChapterCacheDbService, MangaChapterCacheDbService>()
            //Register all roll-up database services      
            .Transient<IDbService, DbService>()
            .Transient<ICacheDbService, CacheDbService>();

        return builder;
    }
}