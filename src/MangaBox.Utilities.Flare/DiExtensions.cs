namespace MangaBox.Utilities.Flare;

/// <summary>
/// Extensions for dependency injection
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the flare solver services
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddFlareSolver(this IServiceCollection services)
    {
        return services
            .AddTransient<IFlareSolverApiService, FlareSolverApiService>()
            .AddTransient<IFlareSolverService,  FlareSolverService>();
    }
}
