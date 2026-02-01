namespace MangaBox.Jwt;

/// <summary>
/// Extensions for dependency injection
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds the JWT services to the dependency resolver
	/// </summary>
	/// <param name="resolver">The resolver to add to</param>
	/// <returns>The resolver fluent method chaining</returns>
	public static IServiceCollection AddJwt(this IServiceCollection resolver)
	{
		return resolver
			.AddSingleton<IJwtKeyService, JwtKeyService>()
			.AddTransient<IJwtTokenService, JwtTokenService>();
	}
}
