using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public sealed class Posting
{
    private Posting(int accountId, decimal amount, CurrencyCode currency)
    {
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
    }

    public int Id { get; private set; }

    public int JournalEntryId { get; private set; }

    public int AccountId { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public int? CategoryId { get; private set; }

    public int? MemberId { get; private set; }

    public decimal FxRate { get; private set; } = 1m;

    public decimal AmountBase { get; private set; }

    public Money Money => Money.Of(Amount, Currency);

    internal static Posting Create(
        int accountId,
        int id,
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
