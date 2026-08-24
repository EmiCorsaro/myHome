using System.Globalization;

namespace MyHome.Modules.Shared.Domain;

/// <summary>
/// An amount together with its currency. The base type of the whole system: no amount ever
/// travels as a bare <see cref="decimal"/>.
/// </summary>
/// <remarks>
/// Three things to know before touching it:
/// <list type="bullet">
/// <item><description>
/// Never <c>double</c>. Binary floating point cannot hold 0.10 exactly and a cent of drift per
/// operation eventually unbalances the ledger. <see cref="decimal"/> here, <c>numeric(19,4)</c>
/// in the database.
/// </description></item>
/// <item><description>
/// Four decimals, not two. Final amounts have two, but splits and prorations need the headroom so
/// rounding happens once, at the end.
/// </description></item>
/// <item><description>
/// Mixing currencies throws. Adding euros to dollars without an exchange rate is a bug, not a
/// case to resolve by guessing.
/// </description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var rent      = Money.Of(1250m, CurrencyCode.Euro);
/// var insurance = Money.Of(148.30m, CurrencyCode.Euro);
///
/// var total = rent + insurance;         // 1398.30 EUR
/// var half  = total.Allocate(2)[0];     // 699.15 EUR, no cents lost
///
/// _ = rent + Money.Of(300m, CurrencyCode.UsDollar);   // InvalidOperationException
/// </code>
/// </example>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>
    /// Decimal places kept during intermediate calculations. Presentation rounds to two; the
    /// domain works with four so that rounding happens only once.
    /// </summary>
    public const int OperatingScale = 4;

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The signed amount. Negative means money leaving.</summary>
    public decimal Amount { get; }

    /// <summary>The currency of the amount.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Whether the amount is exactly zero.</summary>
    public bool IsZero => Amount == 0m;

    /// <summary>
    /// Creates an amount, rounding to <see cref="OperatingScale"/> decimal places.
    /// </summary>
    /// <param name="amount">Signed amount.</param>
    /// <param name="currency">Currency. Must be defined.</param>
    /// <returns>The normalised amount.</returns>
    /// <exception cref="ArgumentException">If the currency is not defined.</exception>
    public static Money Of(decimal amount, CurrencyCode currency)
    {
        if (!currency.IsDefined)
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        return new Money(
            Math.Round(amount, OperatingScale, MidpointRounding.ToEven),
            currency);
    }

    /// <summary>Zero in the given currency.</summary>
    /// <param name="currency">Currency of the amount.</param>
    /// <returns>Zero in that currency.</returns>
    public static Money Zero(CurrencyCode currency) => Of(0m, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <param name="left">First operand.</param>
    /// <param name="right">Second operand.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Of(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts two amounts of the same currency.</summary>
    /// <param name="left">Minuend.</param>
    /// <param name="right">Subtrahend.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Of(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Flips the sign of the amount.</summary>
    /// <param name="value">Amount to negate.</param>
    /// <returns>The amount with its sign reversed.</returns>
    public static Money operator -(Money value) => Of(-value.Amount, value.Currency);

    /// <summary>Multiplies an amount by a dimensionless factor.</summary>
    /// <param name="left">Amount.</param>
    /// <param name="factor">Multiplier.</param>
    /// <returns>The product, in the amount's currency.</returns>
    public static Money operator *(Money left, decimal factor) =>
        Of(left.Amount * factor, left.Currency);

    /// <summary>Whether the left amount is smaller.</summary>
    /// <param name="left">Left amount.</param>
    /// <param name="right">Right amount.</param>
    /// <returns><c>true</c> if the left one is smaller.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>Whether the left amount is greater.</summary>
    /// <param name="left">Left amount.</param>
    /// <param name="right">Right amount.</param>
    /// <returns><c>true</c> if the left one is greater.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>Whether the left amount is smaller than or equal to the right one.</summary>
    /// <param name="left">Left amount.</param>
    /// <param name="right">Right amount.</param>
    /// <returns><c>true</c> if the left one is smaller or equal.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>Whether the left amount is greater than or equal to the right one.</summary>
    /// <param name="left">Left amount.</param>
    /// <param name="right">Right amount.</param>
    /// <returns><c>true</c> if the left one is greater or equal.</returns>
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>Addition, as a method for callers who prefer not to use the operator.</summary>
    /// <param name="other">Amount to add.</param>
    /// <returns>The sum.</returns>
    public Money Add(Money other) => this + other;

    /// <summary>Subtraction, as a method for callers who prefer not to use the operator.</summary>
    /// <param name="other">Amount to subtract.</param>
    /// <returns>The difference.</returns>
    public Money Subtract(Money other) => this - other;

    /// <summary>
    /// Splits the amount into <paramref name="parts"/> shares as equal as possible,
    /// <b>without losing or inventing cents</b>.
    /// </summary>
    /// <param name="parts">Number of shares. Must be greater than zero.</param>
    /// <returns>
    /// The shares, in order. The first ones absorb the remainder, so they may exceed the last
    /// ones by a single minor unit.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="parts"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Splitting 10.00 three ways is not 3.33 three times: that is 9.99 and a cent is missing.
    /// That cent has to go to someone reproducibly, or splits never reconcile. Here it goes to
    /// the first shares; who counts as "first" is the caller's decision, normally the member's
    /// display order.
    /// </remarks>
    /// <example>
    /// <code>
    /// Money.Of(10m, CurrencyCode.Euro).Allocate(3);
    /// // [3.3334  3.3333  3.3333] and the sum is exactly 10.00
    /// </code>
    /// </example>
    public Money[] Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);

        var step = 1m / (decimal)Math.Pow(10, OperatingScale);
        var units = decimal.Round(Amount / step, 0, MidpointRounding.ToEven);

        var baseUnits = decimal.Truncate(units / parts);
        var remainder = units - (baseUnits * parts);

        var result = new Money[parts];
        for (var i = 0; i < parts; i++)
        {
            var extra = i < Math.Abs(remainder) ? Math.Sign(remainder) : 0;
            result[i] = new Money((baseUnits + extra) * step, Currency);
        }

        return result;
    }

    /// <summary>
    /// Rounds to presentation precision. Meant for display, not for further calculation.
    /// </summary>
    /// <param name="decimals">Presentation decimal places. Two by default.</param>
    /// <returns>The rounded amount.</returns>
    public Money RoundForDisplay(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">If the currencies differ.</exception>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.####} {Currency}");

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on amounts in different currencies ({left.Currency} and " +
                $"{right.Currency}). Convert them first with an explicit exchange rate.");
        }
    }
}
