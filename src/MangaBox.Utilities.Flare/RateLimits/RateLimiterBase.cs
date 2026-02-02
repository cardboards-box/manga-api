namespace MangaBox.Utilities.Flare.RateLimits;

/// <summary>
/// Represents a rate limiter for some request
/// </summary>
/// <param name="Limits">The number of requests to allow before waiting</param>
/// <param name="Duration">The number of seconds to wait</param>
public record class RateLimiterBase(
	MinMax Limits,
	MinMax Duration)
{
	/// <summary>
	/// The number of retries to attempt before failing
	/// </summary>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// How long to wait before retrying a request after a 429 code
	/// </summary>
	public MinMax TooManyRequestDuration { get; set; } = new(30, 90);

	/// <summary>
	/// The current count of total requests
	/// </summary>
	public int Count { get; internal set; } = 0;

	/// <summary>
	/// The current count of requests since the last pause
	/// </summary>
	public int Rate { get; internal set; } = 0;

	/// <summary>
	/// Whether or not the rate limiter is enabled
	/// </summary>
	public bool Enabled => Limits.Enabled && Duration.Enabled;

	/// <summary>
	/// Gets the latest rate limit values
	/// </summary>
	/// <returns>The rate limit values</returns>
	public virtual (int limit, int timeout) GetRateLimit()
	{
		return (Limits.Value, Duration.TimeoutMilliseconds);
	}
}
