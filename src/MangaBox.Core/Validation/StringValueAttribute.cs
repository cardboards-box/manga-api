namespace MangaBox.Core.Validation;

/// <summary>
/// Specifies the valid characters that can be used in a string property, field, or parameter.
/// </summary>
/// <param name="validCharacters">The valid characters to allow in the string</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class StringValueAttribute(string validCharacters) : ValidationAttribute(ERROR_MESSAGE)
{
	private const string ERROR_MESSAGE = "The value of \"{0}\" contains invalid characters. Only the following characters are allowed: `{1}`.";

	/// <summary>
	/// The comparison type to use when validating the string
	/// </summary>
	public StringComparison Comparison { get; set; } = StringComparison.CurrentCulture;

	/// <summary>
	/// The valid characters to allow in the string
	/// </summary>
	public string Value { get; } = validCharacters;

	/// <inheritdoc />
	public override string FormatErrorMessage(string name)
	{
		return string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Value);
	}

	/// <inheritdoc />
	public override bool IsValid(object? value)
	{
		if (value is not string str) return true;

		return str.Any(t => Value.Contains(t, Comparison));
	}
}
