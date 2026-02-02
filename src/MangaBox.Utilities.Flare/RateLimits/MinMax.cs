namespace MangaBox.Utilities.Flare.RateLimits;

/// <summary>
/// Represents a min and max range for a value
/// </summary>
/// <param name="min">The minimum value available</param>
/// <param name="max">The maximum value available</param>
public class MinMax(int min, int max)
{
	/// <summary>
	/// The random instance for values
	/// </summary>
	public static Random Rand { get; set; } = new();

	/// <summary>
	/// The minimum value available
	/// </summary>
	public int Min { get; init; } = Math.Min(min, max);

	/// <summary>
	/// The maximum value available
	/// </summary>
	public int Max { get; init; } = Math.Max(min, max);

	/// <summary>
	/// Whether or not the range is "enabled"
	/// </summary>
	public bool Enabled => Min > 0 || Max > 0;

	/// <summary>
	/// The generated value
	/// </summary>
	public int Value => RandomCap();

	/// <summary>
	/// The generated value for timeouts (if given seconds)
	/// </summary>
	public int TimeoutMilliseconds => TimeoutValue();

	/// <summary>
	/// Generates a random number between the min and max values
	/// </summary>
	/// <returns>The random number</returns>
	public virtual int RandomCap()
	{
		var number = Rand.Next(Min - 1, Max + 1);
		return Math.Max(Min, Math.Min(Max, number));
	}

	/// <summary>
	/// Generates a random timeout value between the min and max values
	/// </summary>
	/// <returns>The millisecond value</returns>
	public virtual int TimeoutValue()
	{
		if (!Enabled) return 0;
		if (Max <= Min) return Min;

		var timeoutSec = Value;
		double offset = Rand.NextDouble();
		double dblTimeout = timeoutSec + offset;
		return (int)(dblTimeout * 1000);
	}
}
