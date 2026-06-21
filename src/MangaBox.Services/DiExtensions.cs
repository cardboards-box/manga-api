namespace MangaBox.Services;

using Imaging;
using Queues;

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
			.AddTransient<IListService, ListService>()
			.AddTransient<ICacheService, CacheService>()
			.AddTransient<IImageService, ImageService>()
			.AddSingleton<IStatsService, StatsService>()
			.AddTransient<IApiKeyService, ApiKeyService>()
			.AddSingleton<ISourceService, SourceService>()
			.AddTransient<IVolumeService, VolumeService>()
			.AddTransient<IRelatingService, RelatingService>()
			.AddTransient<IBulkImportService, BulkImportService>()
			.AddSingleton<IFlareImageService, FlareImageService>()
			.AddTransient<IRestitcherService, RestitcherService>()
			.AddTransient<IMangaLoaderService, MangaLoaderService>()
			.AddSingleton<IProxiedHttpService, ProxiedHttpService>()
			.AddSingleton<IMangaPublishService, MangaPublishService>()
			.AddTransient<INotificationService, NotificationService>();
	}
}
