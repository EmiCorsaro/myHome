namespace MyHome.Modules.Shared.Domain;

public readonly record struct CurrencyCode
{
    private readonly string? _value;

    private CurrencyCode(string value) => _value = value;

    public static CurrencyCode Euro { get; } = new("EUR");

    public static CurrencyCode UsDollar { get; } = new("USD");

    public string Value => _value ?? string.Empty;

    public bool IsDefined => !string.IsNullOrEmpty(_value);

    public static CurrencyCode Parse(string value) =>
        TryParse(value, out var currency)
            ? currency
            : throw new ArgumentException(
                $"'{value}' is not a valid ISO 4217 currency code: three letters expected.",
                nameof(value));

    public static bool TryParse(string? value, out CurrencyCode currency)
    {
        currency = default;

        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 3)
        {
            return false;
        }

        foreach (var c in trimmed)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        currency = new CurrencyCode(trimmed.ToUpperInvariant());
        return true;
    }

    public override string ToString() => Value;
}
