namespace MangaBox.Core.Validation;

/// <summary>
/// Validates all of the properties of a class that implements <see cref="IValidator"/>
/// </summary>
public class InnerValidAttribute : ValidationAttribute
{
	/// <inheritdoc />
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value is not IValidator valid)
			return null;

		if (valid.IsValid(out var errors))
			return null;

		var compositeResults = new AggregateValidationResult($"Validation for {validationContext.DisplayName} failed!");
		foreach (var error in errors)
			compositeResults.AddResult(new ValidationResult(error));
		return compositeResults;
	}
}
