namespace MangaBox.Utilities.FCM;

/// <summary>
/// Extensions related to Dependency Injection
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds all of the FCM related services to the DI container
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <param name="section">The section containing the <see cref="FcmOptions"/></param>
	/// <returns>The <see cref="IServiceCollection"/> so additional calls can be chained</returns>
	public static IServiceCollection AddFcmServices(this IServiceCollection services, IConfigurationSection section)
	{
		return services
			.AddSingleton<FcmConnection>()
			.AddTransient<IFcmService, FcmService>()
			.Configure<FcmOptions>(section);
	}

	/// <summary>
	/// Adds all of the FCM related services to the DI container
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <param name="config">The root configuration object</param>
	/// <param name="section">The section containing the <see cref="FcmOptions"/></param>
	/// <returns>The <see cref="IServiceCollection"/> so additional calls can be chained</returns>
	public static IServiceCollection AddFcmServices(this IServiceCollection services, IConfiguration config, string section = "FCM")
	{
		return services.AddFcmServices(config.GetSection(section));
	}
}
