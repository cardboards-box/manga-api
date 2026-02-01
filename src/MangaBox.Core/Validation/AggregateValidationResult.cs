namespace MangaBox.Core.Validation;

/// <summary>
/// Represents a collection of validation results that is treated as a single validation result.
/// </summary>
/// <param name="message">The error message</param>
public class AggregateValidationResult(string? message) : ValidationResult(message)
{
	private readonly List<ValidationResult> _results = [];

	/// <summary>
	/// All of the inner validation results
	/// </summary>
	public IEnumerable<ValidationResult> Results => Expand();

	/// <summary>
	/// Adds the given validation result to the collection
	/// </summary>
	/// <param name="result">The validation result to add</param>
	public void AddResult(ValidationResult result)
	{
		if (result != null && result != Success)
			_results.Add(result);
	}

	/// <summary>
	/// Expands all of the inner validation results into a flat list
	/// </summary>
	/// <returns>All of the validation results</returns>
	public IEnumerable<ValidationResult> Expand()
	{
		foreach (var result in _results)
		{
			if (result is null) continue;

			if (result is not AggregateValidationResult aggregate)
			{
				yield return result;
				continue;
			}

			foreach (var item in aggregate.Expand())
				yield return item;
		}
	}
}
