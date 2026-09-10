namespace GreenExpanses.Domain;

public readonly record struct ConfigVersion
{
    public ConfigVersion(string value)
    {
        Value = Validation.NotBlank(value, nameof(value));
        foreach (var ch in Value)
        {
            if (!(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_'))
            {
                throw new ArgumentException("ConfigVersion contains an unsupported character.", nameof(value));
            }
        }
    }

    public string Value { get; }

    public override string ToString() => Value;
}
