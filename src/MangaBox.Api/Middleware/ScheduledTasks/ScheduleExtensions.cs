namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// Registers the scheduled tasks for the application
/// </summary>
public static class ScheduleExtensions
{
	/// <summary>
	/// Registers the services for the scheduled tasks in the application
	/// </summary>
	/// <param name="services">The service collection to add the scheduled tasks to</param>
	/// <returns>The updated service collection</returns>
	public static IServiceCollection AddScheduledTasks(this IServiceCollection services)
	{
		return services
			.AddScheduler()
			.AddTransient<IndexManga>()
			.AddTransient<MangaRefresh>()
			.AddTransient<BadChapterDelete>()
			.AddTransient<FetchMissingPages>()
			.AddTransient<IndexMissingImages>()
			.AddTransient<StatsSnapshotRefresh>();
	}

	/// <summary>
	/// Adds the scheduled tasks to the application
	/// </summary>
	/// <param name="app">The web applciation to add the scheduled tasks to</param>
	/// <returns>The updated web application</returns>
	public static WebApplication AddScheduledTasks(this WebApplication app)
	{
		app.Services.UseScheduler(schedule =>
		{
			schedule.Schedule<BadChapterDelete>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(BadChapterDelete));

			schedule
				.Schedule<StatsSnapshotRefresh>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(StatsSnapshotRefresh));

			if (app.Environment.IsDevelopment()) return;

			schedule.OnWorker(nameof(MangaRefresh))
				.Schedule<MangaRefresh>()
				.EveryMinute()
				.PreventOverlapping(nameof(MangaRefresh));

			schedule.OnWorker(nameof(FetchMissingPages))
				.Schedule<FetchMissingPages>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(FetchMissingPages));

			schedule.OnWorker(nameof(IndexMissingImages))
				.Schedule<IndexMissingImages>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(IndexMissingImages));

			schedule.OnWorker(nameof(IndexManga))
				.Schedule<IndexManga>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(IndexManga));
		}).LogScheduledTaskProgress();

		return app;
	}

}
