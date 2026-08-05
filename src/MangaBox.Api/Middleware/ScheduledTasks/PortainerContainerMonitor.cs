namespace MangaBox.Api.Middleware.ScheduledTasks;

/// <summary>
/// Monitors configured containers and restarts those that remain outside their desired state.
/// </summary>
public class PortainerContainerMonitor(
    IPortainerService _portainer,
    ILogger<PortainerContainerMonitor> _logger) : ICancellableInvocable, IInvocable
{
    /// <inheritdoc />
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <inheritdoc />
    public async Task Invoke()
    {
        try
        {
            await _portainer.Check(CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Portainer Monitor] An error occurred during portainer monitoring");
        }
    }
}
