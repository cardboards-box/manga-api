namespace MangaBox.Utilities.Auth;

/// <summary>
/// Dependency injection extensions
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the OAuth services
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <returns>The updated service collection</returns>
	public static IServiceCollection AddOAuthServices(this IServiceCollection services)
	{
		return services.AddTransient<IOAuthService, OAuthService>();
	}
}
