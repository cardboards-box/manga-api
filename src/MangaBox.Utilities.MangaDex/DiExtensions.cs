using MangaDexSharp;

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
			.AddMangaDex(c => c
				.WithApiConfig(userAgent: "mb-api"))
			.AddTransient<IMangaDexService, MangaDexService>();
	}
}
