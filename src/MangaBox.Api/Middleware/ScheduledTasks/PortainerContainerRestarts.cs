using Coravel.Scheduling.Schedule.Interfaces;

namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// A scheduled task for restarting Portainer containers on a schedule
/// </summary>
public class PortainerContainerRestarts(
    IPortainerService _portainer,
    ILogger<PortainerContainerRestarts> _logger,
    PortainerContainerOptions _container) : ICancellableInvocable, IInvocable
{
    /// <inheritdoc />
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <inheritdoc />
    public async Task Invoke()
    {
        var name = _container.Container;
        try
        {
            if (_container.Stagger is not null && _container.Stagger.Value > TimeSpan.Zero)
            {
                var stagger = _container.Stagger.Value;
                _logger.LogDebug("[Portainer Restarts] Staggering restart of portainer container {name} by {stagger}", name, stagger);
                await Task.Delay(stagger, CancellationToken);
            }

            _logger.LogDebug("[Portainer Restarts] Restarting portainer container {name}", name);
            await _portainer.Restart(_container, CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Portainer Restarts] An error occurred during portainer container restart for {name}", name);
        }
    }

    /// <summary>
    /// Schedules the Portainer container restarts based on the provided options
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="schedule">The scheduler instance</param>
    public static void Schedule(IServiceProvider services, IScheduler schedule)
    {
        var options = services.GetService<IOptions<PortainerOptions>>();
        if (options is null || options.Value is null || options.Value.Containers.Length == 0) return;

        var restarts = options.Value.Containers.Where(t => t.Restart is not null);
        if (!restarts.Any()) return;

        foreach (var container in restarts)
        {
            var name = $"{nameof(PortainerContainerRestarts)}_{container.Container}";
            schedule.OnWorker(name)
                .ScheduleWithParams<PortainerContainerRestarts>(container)
                .EverySpan(container.Restart!.Value)
                .RunOnceAtStart()
                .PreventOverlapping(name);
        }
    }
}
