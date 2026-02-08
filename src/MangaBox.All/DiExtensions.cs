using CardboardBox.Database.Postgres.Standard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBox.All;

using Core;
using Database;
using Jwt;
using Match;
using Providers;
using Services;
using Utilities.Auth;
using Utilities.Flare;
using Utilities.MangaDex;

/// <summary>
/// Dependency injection services
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds all of the manga box services to the service collection
	/// </summary>
	/// <param name="services">The service collection to add to</param>
	/// <param name="config">The configuration to use</param>
	public static async Task AddMangaBox(this IServiceCollection services, IConfiguration config)
	{
		services
			.AddJwt()
			.AddCoreServices()
			.AddGeneralServices()
			.AddSources()
			.AddFlareSolver()
			.AddOAuthServices(config)
			.AddMatch()
			.AddMangaDex();

		await services.AddServices(config, c => c.AddDatabase());
	}
}
