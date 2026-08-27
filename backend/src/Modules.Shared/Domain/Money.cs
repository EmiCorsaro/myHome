using System.Globalization;

namespace MyHome.Modules.Shared.Domain;

public readonly record struct Money : IComparable<Money>
{
    public const int OperatingScale = 4;

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    public bool IsZero => Amount == 0m;

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

    public static Money Zero(CurrencyCode currency) => Of(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Of(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Of(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value) => Of(-value.Amount, value.Currency);

    public static Money operator *(Money left, decimal factor) =>
        Of(left.Amount * factor, left.Currency);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

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

    public Money RoundForDisplay(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

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
