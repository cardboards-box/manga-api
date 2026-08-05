namespace MangaBox.Services;

/// <summary>
/// Configuration used to monitor containers through a Portainer instance.
/// </summary>
public class PortainerOptions
{
    /// <summary>
    /// Gets or sets the absolute URL of the Portainer instance, including the HTTP or HTTPS scheme.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the Portainer access token sent in the <c>X-API-Key</c> header.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the amount of time a container may remain outside its desired state before it is restarted.
    /// </summary>
    public TimeSpan FailureThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the containers monitored through Portainer.
    /// </summary>
    public PortainerContainerOptions[] Containers { get; set; } = [];
}

/// <summary>
/// Identifies a Portainer-managed container and the state it is expected to maintain.
/// </summary>
public class PortainerContainerOptions
{
    /// <summary>
    /// Gets or sets the Portainer environment (endpoint) identifier that owns the container.
    /// </summary>
    public int EndpointId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Docker container name or identifier.
    /// </summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the desired Docker state. Use <c>healthy</c> to check the health status;
    /// other values, such as <c>running</c>, are compared with the container status.
    /// </summary>
    public string State { get; set; } = "healthy";

    /// <summary>
    /// How often to restart the container
    /// </summary>
    public TimeSpan? Restart { get; set; } = TimeSpan.FromHours(3);

    /// <summary>
    /// When to stagger the restart of the containers to avoid restarting all containers at the same time.
    /// </summary>
    public TimeSpan? Stagger { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Represents the runtime and health states reported by Docker for a container.
/// </summary>
/// <param name="Status">The container runtime status.</param>
/// <param name="Health">The optional container health-check status.</param>
/// <param name="Error">The optional error message</param>
public record class PortainerContainerState(
    string? Status, string? Health, string? Error)
{
    /// <summary>
    /// Determines whether the state matches a configured desired state.
    /// </summary>
    /// <param name="state">The desired state to compare against.</param>
    /// <returns><c>true</c> if the state matches the desired state; otherwise, <c>false</c>.</returns>
    public bool Matches(string state)
    {
        return Health.EqualsIc(state) ||
            Status.EqualsIc(state);
    }
}

/// <summary>
/// Accesses Docker containers through Portainer's environment proxy API.
/// </summary>
public interface IPortainerService
{
    /// <summary>
    /// Get the state of a container through Portainer's environment proxy API.
    /// </summary>
    /// <param name="options">The container options</param>
    /// <param name="token">The cancellation token for the request</param>
    /// <returns>The state of the container</returns>
    Task<PortainerContainerState> State(PortainerContainerOptions options, CancellationToken token);

    /// <summary>
    /// Restart a container through Portainer's environment proxy API.
    /// </summary>
    /// <param name="options">The container options</param>
    /// <param name="token">The cancellation token for the request</param>
    /// <returns><see langword="true"/> if the restart was successful; otherwise, <see langword="false"/>.</returns>
    Task<bool> Restart(PortainerContainerOptions options, CancellationToken token);

    /// <summary>
    /// Checks the state of all configured containers and restarts as needed
    /// </summary>
    /// <param name="token">The cancellation token for the request</param>
    Task Check(CancellationToken token);
}

internal class PortainerService(
    IApiService _api,
    ILogger<PortainerService> _logger,
    IOptionsMonitor<PortainerOptions> _options) : IPortainerService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failures = [];

    public async Task<PortainerContainerState> State(PortainerContainerOptions options, CancellationToken token)
    {
        var (result, error) = await Send<Inspect>(options, HttpMethod.Get, "json", token);
        return error?.ForceNull() is not null
            ? new(null, null, error)
            : new(result?.State?.Status, result?.State?.Health?.Status, null);
    }

    public async Task<bool> Restart(PortainerContainerOptions options, CancellationToken token)
    {
        var error = await Tap(options, HttpMethod.Post, "restart?t=10", token);
        if (error is null) return true;

        _logger.LogError("Failed to restart container {Container} on endpoint {EndpointId}. Error: {Error}",
            options.Container, options.EndpointId, error);
        return false;
    }

    public (IHttpBuilder?, string?) Request(PortainerContainerOptions options, HttpMethod method, string action, CancellationToken token)
    {
        var config = _options.CurrentValue;
        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var baseUri))
            return (default, "Portainer:Url must be an absolute HTTP or HTTPS URL.");
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            return (default, "Portainer:ApiKey is required when container monitoring is configured.");

        var id = Uri.EscapeDataString(options.Container);
        var path = $"api/endpoints/{options.EndpointId}/docker/containers/{id}/{action}";
        var uri = new Uri(baseUri, path);
        return (_api
            .Create(uri.ToString(), null, method.Method, token)
            .Message(c => c.Headers.Add("X-API-Key", config.ApiKey)), null);
    }

    public async Task<(T? result, string? error)> Send<T>(PortainerContainerOptions options, HttpMethod method, string action, CancellationToken token)
    {
        try
        {
            var (client, error) = Request(options, method, action, token);
            if (!string.IsNullOrEmpty(error) || client is null)
                return (default, error);

            using var result = await client.Result();
            if (result is null)
                return (default, "Portainer: failed to get container state.");

            var content = await result.Content.ReadAsStringAsync(token);
            if (!result.IsSuccessStatusCode)
                return (default, $"Portainer: failed to get container state. Status: {result.StatusCode}, Content: {content}");

            var data = JsonSerializer.Deserialize<T>(content);
            if (data is null)
                return (default, $"Portainer: failed to deserialize container state, Content: {content}.");

            return (data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get container state for {Container} on endpoint {EndpointId}.", options.Container, options.EndpointId);
            return (default, ex.Message);
        }
    }

    public async Task<string?> Tap(PortainerContainerOptions options, HttpMethod method, string action, CancellationToken token)
    {
        try
        {
            var (client, error) = Request(options, method, action, token);
            if (!string.IsNullOrEmpty(error) || client is null)
                return error;

            using var result = await client.Result();
            if (result is null)
                return "Portainer: failed to get container state.";

            if (result.IsSuccessStatusCode)
                return null;

            var content = await result.Content.ReadAsStringAsync(token);
            _logger.LogError("Failed to get container state for {Container} on endpoint {EndpointId}. Status: {StatusCode}, Content: {Content}",
                options.Container, options.EndpointId, result.StatusCode, content);
            return $"Portainer: failed to get container state. Status: {result.StatusCode}, Content: {content}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get container state for {Container} on endpoint {EndpointId}.", options.Container, options.EndpointId);
            return ex.Message;
        }
    }

    public async Task Check(CancellationToken token)
    {
        var options = _options.CurrentValue;
        if (options.Containers.Length == 0 ||
            string.IsNullOrWhiteSpace(options.Url) ||
            string.IsNullOrWhiteSpace(options.ApiKey)) return;

        foreach (var container in options.Containers)
        {
            if (token.IsCancellationRequested) break;

            try
            {
                await Check(container, options.FailureThreshold, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Portainer Monitor] Failed to inspect container {Container} on endpoint {EndpointId}",
                    container.Container, container.EndpointId);
            }
        }
    }

    public async Task Check(PortainerContainerOptions container, TimeSpan threshold, CancellationToken token)
    {
        if (container.EndpointId <= 0 || string.IsNullOrWhiteSpace(container.Container) ||
            string.IsNullOrWhiteSpace(container.State))
        {
            _logger.LogWarning("[Portainer Monitor] Ignoring an invalid container configuration");
            return;
        }

        var key = $"{container.EndpointId}:{container.Container}";
        var state = await State(container, token);
        if (state.Matches(container.State))
        {
            if (_failures.TryRemove(key, out _))
                _logger.LogInformation("[Portainer Monitor] Container {Container} returned to {DesiredState}",
                    container.Container, container.State);
            return;
        }

        var failedAt = _failures.GetOrAdd(key, _ => DateTimeOffset.UtcNow);
        if (DateTimeOffset.UtcNow - failedAt < threshold) return;

        _logger.LogWarning(
            "[Portainer Monitor] Container {Container} has status {Status} and health {Health}; restarting after failing to remain {DesiredState} for {Threshold}",
            container.Container, state.Status, state.Health, container.State, threshold);
        await Restart(container, token);
        _failures.TryRemove(key, out _);
    }

    public class Inspect
    {
        public StateData? State { get; set; }

        public class StateData
        {
            public string? Status { get; set; }
            public HealthData? Health { get; set; }
        }

        public class HealthData
        {
            public string? Status { get; set; }
        }
    }
}
