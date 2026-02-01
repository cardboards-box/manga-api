using Microsoft.AspNetCore.Diagnostics;
using OpenTelemetry.Metrics;
using System.IdentityModel.Tokens.Jwt;
using static System.Net.Mime.MediaTypeNames;

namespace MangaBox.Api.Middleware;

internal static class Extensions
{
	/// <summary>
	/// Adds the necessary services for telemetry
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddTelemetry(this IServiceCollection services)
	{
		services.AddOpenTelemetry()
			.WithMetrics(builder =>
			{
				builder.AddPrometheusExporter();

				builder.AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel");
				builder.AddView("http.server.request.duration",
					new ExplicitBucketHistogramConfiguration
					{
						Boundaries =
						[
							0, 0.005, 0.01, 0.025, 0.05,
							0.075, 0.1, 0.25, 0.5, 0.75,
							1, 2.5, 5, 7.5, 10
						]
					});
			});

		return services;
	}

	/// <summary>
	/// Adds the custom JWT authentication services to the application
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddAuthMiddleware(this IServiceCollection services)
	{
		services
			.AddAuthentication(opts => opts.DefaultScheme = AuthMiddleware.SCHEMA)
			.AddScheme<AuthMiddlewareOptions, AuthMiddleware>(AuthMiddleware.SCHEMA, _ => { });
		return services;
	}

	/// <summary>
	/// Adds the necessary handlers for the request boxing
	/// </summary>
	/// <param name="app">The app to register to</param>
	/// <returns>The application builder for fluent method chaining</returns>
	public static IApplicationBuilder RegisterBoxing(this WebApplication app)
	{
		app.UseExceptionHandler(err =>
		{
			err.Run(async ctx =>
			{
				if (ctx.Response.HasStarted) return;

				Exception? resolveException(WebApplication app)
				{
					if (!app.Environment.IsDevelopment()) return null;

					var feature = ctx.Features.Get<IExceptionHandlerFeature>();
					if (feature != null && feature.Error != null)
						return feature.Error;

					feature = ctx.Features.Get<IExceptionHandlerPathFeature>();
					return feature?.Error;
				}
				;

				ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
				ctx.Response.ContentType = Application.Json;
				var error = resolveException(app) ?? new Exception("An error has occurred, please contact an administrator for more information");

				await ctx.Response.WriteAsJsonAsync(Boxed.Exception(error));
			});
		});

		app.Use(async (ctx, next) =>
		{
			await next();

			if (ctx.Response.StatusCode == StatusCodes.Status401Unauthorized && !ctx.Response.HasStarted)
			{
				ctx.Response.ContentType = Application.Json;
				await ctx.Response.WriteAsJsonAsync(Boxed.Unauthorized());
			}
		});

		return app;
	}

	/// <summary>
	/// Gets the ID of the currently logged in user
	/// </summary>
	/// <param name="controller">The controller in question</param>
	/// <returns>The ID of the user (or null if it's invalid or doesn't exist)</returns>
	public static Guid? GetProfileId(this BaseController controller)
	{
		var id = controller.User?.FindFirst(JwtRegisteredClaimNames.NameId)?.Value;
		return !string.IsNullOrEmpty(id) && Guid.TryParse(id, out var iid) ? iid : null;
	}
}
