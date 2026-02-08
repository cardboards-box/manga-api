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
	/// <param name="config">The configuration</param>
	/// <returns>The updated service collection</returns>
	public static IServiceCollection AddOAuthServices(this IServiceCollection services, IConfiguration config)
	{
		return services
			.AddTransient<IOAuthService, OAuthService>()
			.Configure<OAuthOptions>(settings => config.GetSection("OAuth").Bind(settings));
	}
}
