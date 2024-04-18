namespace MangaBox.Core.Requesting;

public class RequestValidator
{
    public List<string> Issues { get; set; } = [];

    public bool Valid => Issues.Count == 0;

    public RequestValidator Check(Func<bool> result, string message)
    {
        if (!result()) Issues.Add(message);
        return this;
    }

    public RequestValidator Check(bool result, string message)
    {
        if (!result) Issues.Add(message);
        return this;
    }

    public RequestValidator NotNull(object? obj, string property)
    {
        if (obj is null) Issues.Add($"{property} cannot be null");
        return this;
    }

    public RequestValidator NotNull(string? obj, string property)
    {
        if (string.IsNullOrEmpty(obj)) Issues.Add($"{property} cannot be null or empty");
        return this;
    }

    public RequestValidator NotWhitespace(string? obj, string property)
    {
        if (string.IsNullOrWhiteSpace(obj)) Issues.Add($"{property} cannot be null or whitespace");
        return this;
    }

    public RequestValidator OneOf(string? value, string property, params string[] options)
    {
        if (options.Contains(value)) Issues.Add($"{property} must be one of {string.Join(", ", options)}");
        return this;
    }

    public RequestValidator IsEnum<T>(string value, string property, out T res) where T: struct, Enum
    {
        if (Enum.TryParse(value, true, out res)) return this;

        Issues.Add($"{property} must be one of {string.Join(", ", Enum.GetNames(typeof(T)))}");
        return this;
    }

    public RequestValidator IsEnum<T>(string value, string property) where T: struct, Enum
    {
        return IsEnum<T>(value, property, out _);
    }

    public RequestValidator GreaterThan(int value, string property, int min)
    {
        if (value > min) return this;

        Issues.Add($"{property} must be greater than {min}");
        return this;
    }

    public RequestValidator GreaterThan(long value, string property, long min)
    {
        if (value > min) return this;

        Issues.Add($"{property} must be greater than {min}");
        return this;
    }

    public RequestValidator GreaterThan(float value, string property, float min)
    {
        if (value > min) return this;

        Issues.Add($"{property} must be greater than {min}");
        return this;
    }

    public RequestValidator GreaterThan(double value, string property, double min)
    {
        if (value > min) return this;

        Issues.Add($"{property} must be greater than {min}");
        return this;
    }

    public RequestValidator LessThan(int value, string property, int max)
    {
        if (value < max) return this;

        Issues.Add($"{property} must be less than {max}");
        return this;
    }

    public RequestValidator LessThan(long value, string property, long max)
    {
        if (value < max) return this;

        Issues.Add($"{property} must be less than {max}");
        return this;
    }

    public RequestValidator LessThan(float value, string property, float max)
    {
        if (value < max) return this;

        Issues.Add($"{property} must be less than {max}");
        return this;
    }

    public RequestValidator LessThan(double value, string property, double max)
    {
        if (value < max) return this;

        Issues.Add($"{property} must be less than {max}");
        return this;
    }

    public RequestValidator Between(int value, string property, int min, int max)
    {
        if (value > min && value < max) return this;

        Issues.Add($"{property} must be between {min} and {max}");
        return this;
    }

    public RequestValidator Between(long value, string property, long min, long max)
    {
        if (value > min && value < max) return this;

        Issues.Add($"{property} must be between {min} and {max}");
        return this;
    }

    public RequestValidator Between(float value, string property, float min, float max)
    {
        if (value > min && value < max) return this;

        Issues.Add($"{property} must be between {min} and {max}");
        return this;
    }

    public RequestValidator Between(double value, string property, double min, double max)
    {
        if (value > min && value < max) return this;

        Issues.Add($"{property} must be between {min} and {max}");
        return this;
    }

    public RequestValidator IsGuid(string value, string property, out Guid res)
    {
        if (Guid.TryParse(value, out res)) return this;
        
        Issues.Add($"{property} must be a valid GUID");
        return this;
    }

    public bool Validate(out Boxed boxed)
    {
        if (Issues.Count == 0)
        {
            boxed = Boxed.Ok();
            return true;
        }

        boxed = Boxed.Bad(Issues);
        return false;
    }
}
