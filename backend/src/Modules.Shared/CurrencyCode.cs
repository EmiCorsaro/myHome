namespace MyHome.Modules.Shared;

/// <summary>
/// A three-letter ISO 4217 currency code, always uppercase.
/// </summary>
/// <remarks>
/// It is a dedicated type rather than a <see cref="string"/> for one concrete reason: it makes
/// it impossible to build a <see cref="Money"/> with a misspelled or invented currency. The
/// system is multi-currency by design (foreign-currency income and assets), so the currency is
/// not decoration on the amount: it is part of its identity.
/// </remarks>
/// <example>
/// <code>
/// var eur = CurrencyCode.Euro;
/// var usd = CurrencyCode.Parse("usd");   // normalised to "USD"
///
/// if (CurrencyCode.TryParse(input, out var currency)) { /* ... */ }
/// </code>
/// </example>
public readonly record struct CurrencyCode
{
    private readonly string? _value;

    private CurrencyCode(string value) => _value = value;

    /// <summary>Euro. The system's base currency unless a household declares another.</summary>
    public static CurrencyCode Euro { get; } = new("EUR");

    /// <summary>United States dollar.</summary>
    public static CurrencyCode UsDollar { get; } = new("USD");

    /// <summary>
    /// The three uppercase letters. Empty string if the struct was created via
    /// <c>default</c>, which no code should do.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Whether this instance holds a valid code.</summary>
    public bool IsDefined => !string.IsNullOrEmpty(_value);

    /// <summary>
    /// Converts text into a <see cref="CurrencyCode"/>, normalising to uppercase.
    /// </summary>
    /// <param name="value">Three ASCII letters. Lowercase and surrounding whitespace are accepted.</param>
    /// <returns>The normalised code.</returns>
    /// <exception cref="ArgumentException">
    /// If the text is not exactly three ASCII letters.
    /// </exception>
    public static CurrencyCode Parse(string value) =>
        TryParse(value, out var currency)
            ? currency
            : throw new ArgumentException(
                $"'{value}' is not a valid ISO 4217 currency code: three letters expected.",
                nameof(value));

    /// <summary>
    /// Attempts to convert text into a <see cref="CurrencyCode"/> without throwing.
    /// </summary>
    /// <param name="value">Input text; lowercase and surrounding whitespace are accepted.</param>
    /// <param name="currency">The normalised code if parsing succeeded.</param>
    /// <returns><c>true</c> if the text was a valid code.</returns>
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

    /// <inheritdoc />
    public override string ToString() => Value;
}
