namespace MangaBox.Match;

using RIS;
using SauceNao;

/// <summary>
/// Dependency injection extensions
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Generates a generic rate limiter
	/// </summary>
	/// <param name="releases">The number of active leases</param>
	/// <param name="span">The replenishment period</param>
	/// <returns>The rate limiter</returns>
	private static TokenBucketRateLimiter GenericRateLimiter(int releases, TimeSpan span) => new(new()
	{
		TokenLimit = releases,
		QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
		QueueLimit = int.MaxValue,
		ReplenishmentPeriod = span,
		TokensPerPeriod = releases,
		AutoReplenishment = true
	});

	/// <summary>
	/// Adds the mangabox match services for reverse image search
	/// </summary>
	/// <param name="services">The service collection to append to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddMatch(this IServiceCollection services)
	{
		return services
			.AddTransient<IReverseImageSearchService, ReverseImageSearchService>()

			//Add internal RIS service
			.AddTransient<IRISApiService, RISApiService>()
			.AddTransient<IImageSearchService, MatchSearchService>()

			//Add sauce nao
			.AddKeyedSingleton<RateLimiter>(
				SauceNaoSearchService.LIMITER_KEY, 
				GenericRateLimiter(6, TimeSpan.FromSeconds(5)))
			.AddTransient<ISauceNaoApiService, SauceNaoApiService>()
			.AddTransient<IImageSearchService, SauceNaoSearchService>()
			
			//Add google vsion
			.AddTransient<IImageSearchService, VisionSearchService>();
	}
}
