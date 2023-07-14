namespace CardboardBox.Manga;

using Sources;

public static class Extensions
{
    public static IDependencyBuilder AddMangaSources(this IDependencyBuilder builder)
    {
        return builder
            .Transient<IMangaImportService, MangaImportService>()

            .Transient<INhentaiSource, NHentaiSource>()
            .Transient<IMangaKatanaSource, MangaKatanaSource>()
            .Transient<IMangakakalotTvSource, MangakakalotTvSource>()
            .Transient<IMangakakalotComSource, MangakakalotComSource>()
            .Transient<IMangakakalotComAltSource, MangakakalotComAltSource>()
            .Transient<IMangaDexSource, MangaDexSource>()
            .Transient<IMangaClashSource, MangaClashSource>();
    }
}
