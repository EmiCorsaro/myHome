using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public sealed class Posting : Entity
{
    private Posting(int accountId, decimal amount, CurrencyCode currency)
    {
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
    }

    public int JournalEntryId { get; private set; }

    public int AccountId { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public int? CategoryId { get; private set; }

    public int? MemberId { get; private set; }

    public decimal FxRate { get; private set; } = 1m;

    public decimal AmountBase { get; private set; }

    public Money Money => Money.Of(Amount, Currency);

    /// <summary>
    /// Restates how much this posting moves, keeping everything else about it.
    /// </summary>
    /// <param name="amount">The amount the posting should have carried all along.</param>
    /// <remarks>
    /// Only an opening balance is corrected this way: it states a position rather than recording
    /// something that happened, so there is nothing to reverse.
    /// </remarks>
    internal void Restate(Money amount)
    {
        Amount = amount.Amount;
        Currency = amount.Currency;
        AmountBase = amount.Amount;
    }

    internal static Posting Create(
        int accountId,
        Money amount,
        int? categoryId = null,
        int? memberId = null)
    {
        return new Posting(
            accountId,
            amount.Amount,
            amount.Currency)
        {
            CategoryId = categoryId,
            MemberId = memberId,
            FxRate = 1m,
            AmountBase = amount.Amount,
        };
    }
}
