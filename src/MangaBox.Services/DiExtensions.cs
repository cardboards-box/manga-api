namespace MangaBox.Services;

/// <summary>
/// Extensions for dependency injection
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the general services
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddGeneralServices(this IServiceCollection services)
	{
		return services
			.AddSingleton<IZipService, ZipService>()
			.AddTransient<IHttpService, HttpService>()
			.AddTransient<ICacheService, CacheService>()
			.AddSingleton<IStatsService, StatsService>()
			.AddTransient<IImageService, ImageService>()
			.AddTransient<IVolumeService, VolumeService>()
			.AddSingleton<ISourceService, SourceService>()
			.AddTransient<IMangaLoaderService, MangaLoaderService>();
	}
}
