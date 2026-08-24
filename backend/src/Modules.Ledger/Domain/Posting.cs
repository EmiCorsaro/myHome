using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// One side of an entry: an amount landing on one account.
/// </summary>
/// <remarks>
/// One rule for the sign: positive means the account receives, negative means it gives. Paying
/// 42 € by card is −42 on the bank account and +42 on the expense account. Postings are only ever
/// created by their entry, so an orphan cannot exist to unbalance the books.
/// </remarks>
public sealed class Posting
{
    private Posting(
        Guid id,
        Guid journalEntryId,
        Guid accountId,
        decimal amount,
        CurrencyCode currency)
    {
        Id = id;
        JournalEntryId = journalEntryId;
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Posting identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Entry this posting belongs to.</summary>
    public Guid JournalEntryId { get; private set; }

    /// <summary>Account receiving the posting.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Signed amount, in the posting's currency. Positive: the account receives. Negative: the
    /// account gives.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency of the amount.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>
    /// Category classifying this posting, when there is one. Only nominal postings carry it: an
    /// outflow from the bank account has no category, the expense it funds does.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Member the posting is attributed to, when it makes sense. Household expenses paid from a
    /// joint account leave it <see langword="null"/>.
    /// </summary>
    public Guid? MemberId { get; private set; }

    /// <summary>
    /// Rate used to convert to the household's base currency. Always filled in, and 1 while
    /// everything is in the same currency.
    /// </summary>
    /// <remarks>
    /// Stored even while it is always 1. When a second currency turns up, the column is there and
    /// old rows do not need a rate nobody can reconstruct.
    /// </remarks>
    public decimal FxRate { get; private set; } = 1m;

    /// <summary>Equivalent amount in the household's base currency.</summary>
    public decimal AmountBase { get; private set; }

    /// <summary>The amount together with its currency.</summary>
    public Money Money => Money.Of(Amount, Currency);

    /// <summary>
    /// Creates a posting. Internal so only <see cref="JournalEntry"/> can call it, which is what
    /// keeps every posting inside a balanced entry.
    /// </summary>
    /// <param name="journalEntryId">Entry it belongs to.</param>
    /// <param name="accountId">Account receiving it.</param>
    /// <param name="amount">Signed amount.</param>
    /// <param name="categoryId">Classifying category, if any.</param>
    /// <param name="memberId">Member it is attributed to, if any.</param>
    /// <returns>The new posting.</returns>
    internal static Posting Create(
        Guid journalEntryId,
        Guid accountId,
        Money amount,
        Guid? categoryId = null,
        Guid? memberId = null)
    {
        return new Posting(
            Guid.CreateVersion7(),
            journalEntryId,
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
