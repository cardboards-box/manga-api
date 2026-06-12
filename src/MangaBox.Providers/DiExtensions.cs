using WeebDexSharp;

namespace MangaBox.Providers;

using Sources;

public static class DiExtensions
{
	private static IServiceCollection AddSource<TService, TImplementation>(this IServiceCollection services)
		where TService : class, IMangaSource
		where TImplementation : class, TService
	{
		return services
			.AddTransient<IMangaSource, TImplementation>()
			.AddTransient<TService, TImplementation>();
	}

	public static IServiceCollection AddSources(this IServiceCollection services)
	{
		return services
			//Register everything MangaDex related
			.AddSource<IMangaDexSource, MangaDexSource>()

			//Register everything WeebDex related
			.AddWeebDex(c => c.WithCredentialsApiKey(string.Empty, string.Empty))
			.AddSource<IWeebDexSource, WeebDexSource>()

			//Register other sources
			.AddSource<IComixSource, ComixSource>()
			.AddSource<IMangakakalotTvSource, MangakakalotTvSource>()
			.AddSource<IMangakakalotComSource, MangakakalotComSource>()
			.AddSource<IMangakakalotComAltSource, MangakakalotComAltSource>()
			.AddSource<IMangaClashSource, MangaClashSource>()
			.AddSource<IMangaFireSource, MangaFireSource>()
			.AddSource<IMangaReadSource, MangaReadSource>()
			.AddSource<INhentaiSource, NhentaiSource>()
			.AddSource<INhentaiNetSource, NhentaiNetSource>()
			.AddSource<IKappaBeastSource, KappaBeastSource>()
			.AddSource<IMangaKatanaSource, MangaKatanaSource>()
			.AddSource<IDarkScansSource, DarkScansSource>()
			.AddSource<IChapmanganatoSource, ChapmanganatoSource>()
			.AddSource<ILikeMangaSource, LikeMangaSource>()
			.AddSource<ILilyMangaSource, LilyMangaSource>()
			.AddSource<IHyakuroSource, HyakuroSource>();
	}
}
