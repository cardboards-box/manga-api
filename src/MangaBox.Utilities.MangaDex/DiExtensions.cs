using MangaDexSharp;
using System.Threading.RateLimiting;

namespace MangaBox.Utilities.MangaDex;

/// <summary>
/// Dependency injection extensions
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the mangadex services to the service collection
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddMangaDex(this IServiceCollection services)
	{
		return services
			.AddMangaDex(c => c.WithApiConfig(userAgent: "mb-api"))
			.AddTransient<IMangaDexService, MangaDexService>()
			.AddKeyedSingleton<RateLimiter>(MangaDexService.KEY, new TokenBucketRateLimiter(new()
			{
				TokenLimit = 5,
				TokensPerPeriod = 5,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = int.MaxValue,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1),
				AutoReplenishment = true
			}));
	}
}
