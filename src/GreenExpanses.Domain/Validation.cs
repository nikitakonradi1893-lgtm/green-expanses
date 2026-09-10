namespace GreenExpanses.Domain;

public static class Validation
{
    public static string NotBlank(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);
        }

        return value;
    }

    public static decimal NonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value cannot be negative.");
        }

        return value;
    }

    public static decimal InRange(decimal value, decimal minimum, decimal maximum, string parameterName)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.", nameof(minimum));
        }

        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be in range [{minimum}, {maximum}].");
        }

        return value;
    }
}
