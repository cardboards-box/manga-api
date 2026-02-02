using CardboardBox.Redis;

namespace MangaBox.Core;

/// <summary>
/// DI extensions for the core services
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the core services to the service collection
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddCoreServices(this IServiceCollection services)
	{
		return services
			.AddRedis()
			.AddSingleton<IQueryCacheService, QueryCacheService>();
	}
}
