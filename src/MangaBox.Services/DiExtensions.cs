namespace MangaBox.Services;

/// <summary>
/// Extensions for dependency injection
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the general theuth services
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddGeneralServices(this IServiceCollection services)
	{
		return services
			.AddTransient<IMangaLoaderService, MangaLoaderService>();
	}
}
