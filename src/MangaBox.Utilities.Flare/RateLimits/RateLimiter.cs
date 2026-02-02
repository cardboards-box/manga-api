using System.Runtime.CompilerServices;

namespace MangaBox.Utilities.Flare.RateLimits;

/// <summary>
/// Represents a rate limiter for some request
/// </summary>
/// <typeparam name="T">The type of result returned by the request</typeparam>
/// <param name="Limits">The number of requests to allow before waiting</param>
/// <param name="Duration">The number of seconds to wait</param>
/// <param name="Fetcher">How the request occurs</param>
public record class RateLimiter<T>(
	MinMax Limits,
	MinMax Duration,
	Func<Task<T>> Fetcher) : RateLimiterBase(Limits, Duration)
{
	/// <summary>
	/// Make a request using the <see cref="Fetcher"/> and retries if a 429 is returned
	/// </summary>
	/// <param name="logger">The logger for messages</param>
	/// <param name="token">The cancellation token for the requests</param>
	/// <param name="count">The number of requests already been made</param>
	/// <returns>The result of the request</returns>
	public virtual async Task<T> MakeRequest(ILogger logger, CancellationToken token, int count = 0)
	{
		try
		{
			return await Fetcher();
		}
		catch (HttpRequestException ex)
		{
			if (ex.StatusCode != HttpStatusCode.TooManyRequests)
			{
				logger.LogError(ex, "Failed to fetch data - HTTP Exception");
				throw;
			}

			if (count >= MaxRetries)
			{
				logger.LogError(ex, "Failed to fetch data after {count} retries", count);
				throw;
			}

			var timeout = TooManyRequestDuration.TimeoutMilliseconds;
			logger.LogWarning("Too many requests, waiting {timeout}ms before retrying", timeout);
			await Task.Delay(timeout, token);
			logger.LogInformation("Retrying request after Too many requests");
			return await MakeRequest(logger, token, count + 1);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to fetch data");
			throw;
		}
	}

	/// <summary>
	/// Fetches the data from the endpoint using the rate limiter
	/// </summary>
	/// <param name="logger">The logger to use for messages</param>
	/// <param name="token">The cancellation token for stopping iteration</param>
	/// <returns>All of the records from the request</returns>
	public async IAsyncEnumerable<T> Fetch(ILogger logger, [EnumeratorCancellation] CancellationToken token)
	{
		Count = 0;
		Rate = 0;
		var (limit, timeout) = GetRateLimit();

		while (true)
		{
			if (token.IsCancellationRequested) yield break;

			Count++;
			Rate++;

			var result = await MakeRequest(logger, token);
			yield return result;

			if (!Enabled || Rate < limit) continue;

			logger.LogInformation("Rate limit reached. Pausing for {timeout}ms. Count: {count} - {rate}/{limit}",
				timeout, Count, Rate, limit);

			await Task.Delay(timeout, token);
			Rate = 0;
			(limit, timeout) = GetRateLimit();

			logger.LogInformation("Resuming after pause. New Limits {limit} - {timeout}ms", limit, timeout);
		}
	}
}

/// <summary>
/// Represents a rate limiter for some request
/// </summary>
/// <typeparam name="TIn"></typeparam>
/// <typeparam name="TOut"></typeparam>
/// <param name="Limits">The number of requests to allow before waiting</param>
/// <param name="Duration">The number of seconds to wait</param>
/// <param name="Fetcher">How the request occurs</param>
public record class RateLimiter<TIn, TOut>(
	MinMax Limits,
	MinMax Duration,
	Func<TIn, Task<TOut>> Fetcher) : RateLimiterBase(Limits, Duration)
{
	/// <summary>
	/// Make a request using the <see cref="Fetcher"/> and retries if a 429 is returned
	/// </summary>
	/// <param name="item">The item to fetch for</param>
	/// <param name="logger">The logger for messages</param>
	/// <param name="token">The cancellation token for the requests</param>
	/// <param name="count">The number of requests already been made</param>
	/// <returns>The result of the request</returns>
	public virtual async Task<TOut> MakeRequest(TIn item, ILogger logger, CancellationToken token, int count = 0)
	{
		try
		{
			return await Fetcher(item);
		}
		catch (HttpRequestException ex)
		{
			if (ex.StatusCode != HttpStatusCode.TooManyRequests)
			{
				logger.LogError(ex, "Failed to fetch data - HTTP Exception");
				throw;
			}

			if (count >= MaxRetries)
			{
				logger.LogError(ex, "Failed to fetch data after {count} retries", count);
				throw;
			}

			var timeout = TooManyRequestDuration.TimeoutMilliseconds;
			logger.LogWarning("Too many requests, waiting {timeout}ms before retrying", timeout);
			await Task.Delay(timeout, token);
			logger.LogInformation("Retrying request after Too many requests");
			return await MakeRequest(item, logger, token, count + 1);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to fetch data");
			throw;
		}
	}

	/// <summary>
	/// Fetches the data from the endpoint using the rate limiter
	/// </summary>
	/// <param name="input">The input data</param>
	/// <param name="logger">The logger to use for messages</param>
	/// <param name="token">The cancellation token for stopping iteration</param>
	/// <returns>All of the records from the request</returns>
	public async IAsyncEnumerable<TOut> Fetch(IAsyncEnumerable<TIn> input, ILogger logger, [EnumeratorCancellation] CancellationToken token)
	{
		Count = 0;
		Rate = 0;
		var (limit, timeout) = GetRateLimit();

		await foreach (var item in input)
		{
			if (token.IsCancellationRequested) yield break;

			if (Enabled && Rate >= limit)
			{
				logger.LogInformation("Rate limit reached. Pausing for {timeout}ms. Count: {count} - {rate}/{limit}",
					timeout, Count, Rate, limit);

				await Task.Delay(timeout, token);
				Rate = 0;
				(limit, timeout) = GetRateLimit();

				logger.LogInformation("Resuming after pause. New Limits {limit} - {timeout}ms", limit, timeout);
			}

			Count++;
			Rate++;

			var result = await MakeRequest(item, logger, token);
			yield return result;
		}
	}
}