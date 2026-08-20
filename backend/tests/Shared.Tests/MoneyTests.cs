using MyHome.Modules.Shared;

namespace MyHome.Modules.Shared.Tests;

/// <summary>
/// Tests for <see cref="Money"/>. It is the type everything else hangs from: if a cent is lost
/// here, it is lost across the whole system.
/// </summary>
public sealed class MoneyTests
{
    [Fact(DisplayName = "Adding different currencies is an error, not an implicit conversion")]
    public void adding_different_currencies_throws()
    {
        var euros = Money.Of(100m, CurrencyCode.Euro);
        var dollars = Money.Of(100m, CurrencyCode.UsDollar);

        Assert.Throws<InvalidOperationException>(() => euros + dollars);
    }

    [Theory(DisplayName = "An allocation never loses or invents a cent")]
    [InlineData(10, 3)]
    [InlineData(100, 3)]
    [InlineData(0.01, 3)]
    [InlineData(1398.30, 7)]
    [InlineData(-42.55, 4)]
    public void allocation_sums_back_to_the_total(decimal amount, int parts)
    {
        var total = Money.Of(amount, CurrencyCode.Euro);

        var pieces = total.Allocate(parts);

        Assert.Equal(parts, pieces.Length);
        Assert.Equal(total, pieces.Aggregate(Money.Zero(CurrencyCode.Euro), (a, b) => a + b));
    }

    [Fact(DisplayName = "The leftover cent goes to the first shares, always the same way")]
    public void the_remainder_is_distributed_deterministically()
    {
        var pieces = Money.Of(10m, CurrencyCode.Euro).Allocate(3);

        Assert.Equal(3.3334m, pieces[0].Amount);
        Assert.Equal(3.3333m, pieces[1].Amount);
        Assert.Equal(3.3333m, pieces[2].Amount);
    }

    [Fact(DisplayName = "Allocating into zero parts makes no sense and fails")]
    public void allocating_into_zero_parts_throws()
    {
        var total = Money.Of(10m, CurrencyCode.Euro);

        Assert.Throws<ArgumentOutOfRangeException>(() => total.Allocate(0));
    }

    [Fact(DisplayName = "An amount is rounded to four decimals on creation")]
    public void amounts_are_normalised_to_the_operating_scale()
    {
        var value = Money.Of(1.234567m, CurrencyCode.Euro);

        Assert.Equal(1.2346m, value.Amount);
    }

    [Fact(DisplayName = "An amount cannot be built without a currency")]
    public void currency_is_required()
    {
        Assert.Throws<ArgumentException>(() => Money.Of(10m, default));
    }
}
