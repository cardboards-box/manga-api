using Coravel.Scheduling.Schedule.Interfaces;

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
			.AddTransient<LogCleanup>()
			.AddTransient<MangaRefresh>()
			.AddTransient<BadChapterDelete>()
			.AddTransient<FetchMissingPages>()
			.AddTransient<IndexMissingImages>()
			.AddTransient<StatsSnapshotRefresh>();
	}

	/// <summary>
	/// Converts the timespan to a cron expression
	/// </summary>
	/// <param name="schedule">The schedule to append to</param>
	/// <param name="span">The timespan to convert to a cron expression</param>
	/// <returns>The updated schedule with the cron expression</returns>
	public static IScheduledEventConfiguration EverySpan(this IScheduleInterval schedule, TimeSpan span)
	{
		if (span <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(span), span, "The schedule frequency must be greater than zero.");

		var seconds = (int)Math.Ceiling(span.TotalSeconds);
		if (seconds <= 60 || span.Seconds != 0 || span.Milliseconds != 0)
			return schedule.EverySeconds(seconds);

		var minutes = (int)span.TotalMinutes;
		if (minutes < 60)
			return schedule.Cron($"*/{minutes} * * * *");

		if (span.Minutes != 0)
			return schedule.EverySeconds(seconds);

		var hours = (int)span.TotalHours;
		if (hours < 24)
			return schedule.Cron($"0 */{hours} * * *");

		if (span.Hours != 0)
			return schedule.EverySeconds(seconds);

		var days = Math.Max(1, (int)span.TotalDays);
		var cron = days == 1
			? "0 0 * * *"
			: $"0 0 */{days} * *";

		return schedule.Cron(cron);
	}

	/// <summary>
	/// Adds the scheduled tasks to the application
	/// </summary>
	/// <param name="app">The web applciation to add the scheduled tasks to</param>
	/// <returns>The updated web application</returns>
	public static async Task AddScheduledTasks(this WebApplication app)
	{
		var loader = app.Services.GetRequiredService<IMangaLoaderService>();
		var indexables = await loader.GetIndexableSources(default);

		app.Services.UseScheduler(schedule =>
		{
			schedule.Schedule<BadChapterDelete>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(BadChapterDelete));

			schedule
				.Schedule<StatsSnapshotRefresh>()
				.EveryThirtySeconds()
				.PreventOverlapping(nameof(StatsSnapshotRefresh));

			schedule
				.Schedule<LogCleanup>()
				.Daily() 
				.PreventOverlapping(nameof(LogCleanup));

			foreach (var (source, frequency, name) in indexables)
			{
				var fullName = $"{nameof(IndexManga)}-{name}";
				schedule.OnWorker(fullName)
					.ScheduleWithParams<IndexManga>(source)
					.EverySpan(frequency)
					.RunOnceAtStart()
					.PreventOverlapping(fullName);
			}

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
		}).LogScheduledTaskProgress();
	}

}
