using System.Globalization;

namespace GreenExpanses.Domain;

public readonly record struct EntityId(Guid Value)
{
    public static EntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

public readonly record struct CatalogId
{
    public CatalogId(string value)
    {
        Value = Validation.NotBlank(value, nameof(value));
        if (!IsSnakeCase(Value))
        {
            throw new ArgumentException("CatalogId must be lowercase snake_case.", nameof(value));
        }
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static bool IsSnakeCase(string value)
    {
        if (value.Length == 0 || value[0] == '_' || value[^1] == '_')
        {
            return false;
        }

        var previousUnderscore = false;
        foreach (var ch in value)
        {
            var isValid = ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '_';
            if (!isValid || ch == '_' && previousUnderscore)
            {
                return false;
            }

            previousUnderscore = ch == '_';
        }

        return true;
    }
}

public readonly record struct GameDateTime
{
    public GameDateTime(DateTime value)
    {
        Value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    public DateTime Value { get; }

    public GameDateTime AddDays(int days) => new(Value.AddDays(days));

    public override string ToString() => Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}

public readonly record struct Money(decimal Value)
{
    public static Money Zero => new(0m);

    public static Money operator +(Money left, Money right) => new(left.Value + right.Value);
    public static Money operator -(Money left, Money right) => new(left.Value - right.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct AreaHa
{
    public AreaHa(decimal value)
    {
        Value = Validation.NonNegative(value, nameof(value));
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct MassT
{
    public MassT(decimal value)
    {
        Value = Validation.NonNegative(value, nameof(value));
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct Ratio
{
    public Ratio(decimal value)
    {
        Value = Validation.InRange(value, 0m, 1m, nameof(value));
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
