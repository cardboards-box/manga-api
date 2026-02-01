namespace MangaBox.Core.Validation;

/// <summary>
/// Specifies the maximum value that a property, field, or parameter can have.
/// </summary>
/// <param name="value">The maximum value that the property, field, or parameter can have.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class MaxValueAttribute(double value) : ValidationAttribute(ERROR_MESSAGE)
{
	private const string ERROR_MESSAGE = "The value of \"{0}\" must be less than or equal to {1}.";

	/// <summary>
	/// The maximum value that the property, field, or parameter can have.
	/// </summary>
	public double Value { get; } = value;

	/// <inheritdoc />
	public override string FormatErrorMessage(string name)
	{
		return string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Value);
	}

	/// <inheritdoc />
	public override bool IsValid(object? value)
	{
		if (value == null) return true;

		if (value is double doubleValue)
			return doubleValue <= Value;

		try
		{
			var convertedValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
			return convertedValue <= Value;
		}
		catch
		{
			return false;
		}
	}
}
