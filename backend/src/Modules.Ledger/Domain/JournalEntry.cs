using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// What an entry represents, from the household's point of view.
/// </summary>
public enum EntryKind
{
    /// <summary>Money entering the household.</summary>
    Income = 1,

    /// <summary>Money leaving the household.</summary>
    Expense = 2,

    /// <summary>Money moving between the household's own accounts. Net worth unchanged.</summary>
    Transfer = 3,

    /// <summary>Starting balance of an account, recorded when it is first set up.</summary>
    Opening = 4,
}

/// <summary>
/// One economic fact, recorded as the set of postings that make it up.
/// </summary>
/// <remarks>
/// Aggregate root of the ledger, and where invariant I-1 lives: the postings of an entry sum to
/// zero per currency. The factory methods below are the only way to build one and they all check
/// before returning, so an unbalanced entry cannot reach the database.
/// <para>
/// The payoff is that balance, spend-by-category and projection are the same operation with
/// different filters, and the awkward cases (a card purchase that is a March expense but an April
/// outflow) need no special handling.
/// </para>
/// </remarks>
public sealed class JournalEntry
{
    private readonly List<Posting> _postings = [];

    private JournalEntry(
        Guid id,
        Guid householdId,
        EntryKind kind,
        DateOnly occurredOn,
        string description,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        Kind = kind;
        OccurredOn = occurredOn;
        Description = description;
        CreatedAt = createdAt;
    }

    /// <summary>Entry identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Household the entry belongs to.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>What the entry represents.</summary>
    public EntryKind Kind { get; private set; }

    /// <summary>
    /// The date the fact happened, which is not necessarily the date it was recorded.
    /// </summary>
    /// <remarks>
    /// <see cref="DateOnly"/> and not a timestamp. A purchase happens on a day; a time zone would
    /// only raise the question of which month a late-night purchase falls in.
    /// </remarks>
    public DateOnly OccurredOn { get; private set; }

    /// <summary>What it was, in the household's own words.</summary>
    public string Description { get; private set; }

    /// <summary>When the entry was recorded.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Identifier supplied by the client to make registration idempotent.
    /// </summary>
    /// <remarks>
    /// A dropped connection means the user presses save again. With this, the second request
    /// finds the first one and returns it instead of recording the groceries twice.
    /// </remarks>
    public string? ClientMutationId { get; private set; }

    /// <summary>
    /// Rule that predicted this entry, when it came from one. <see langword="null"/> for one-off
    /// movements.
    /// </summary>
    public Guid? RecurringRuleId { get; private set; }

    /// <summary>The postings making up the entry. Always at least two, always summing to zero.</summary>
    public IReadOnlyList<Posting> Postings => _postings;

    /// <summary>
    /// Records an expense: money leaving a real account towards an expense account, classified
    /// by a category.
    /// </summary>
    /// <param name="householdId">Household recording it.</param>
    /// <param name="occurredOn">Date the expense happened.</param>
    /// <param name="description">What it was. Required.</param>
    /// <param name="paidFrom">
    /// Account the money leaves. Must be a real account: paying an expense out of another
    /// expense account is not a thing.
    /// </param>
    /// <param name="expenseAccount">Nominal account the expense accumulates in.</param>
    /// <param name="category">Category classifying it. Must be an expense category.</param>
    /// <param name="amount">Amount spent. Must be positive: the direction is the entry's job.</param>
    /// <param name="memberId">Member it is attributed to, if any.</param>
    /// <param name="clientMutationId">Idempotency key supplied by the client.</param>
    /// <param name="recurringRuleId">Rule this expense belongs to, if it is a recurring one.</param>
    /// <param name="createdAt">Recording instant. Defaults to now.</param>
    /// <returns>The balanced entry, with its two postings.</returns>
    /// <exception cref="ArgumentException">If the amount is not positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// If the accounts or the category do not fit together: wrong household, wrong type, or a
    /// currency that is not the paying account's.
    /// </exception>
    /// <example>
    /// <code>
    /// var entry = JournalEntry.RegisterExpense(
    ///     householdId,
    ///     new DateOnly(2026, 8, 14),
    ///     "Weekly shop",
    ///     paidFrom: santander,
    ///     expenseAccount: expenses,
    ///     category: groceries,
    ///     amount: Money.Of(42.35m, CurrencyCode.Euro));
    ///
    /// // entry.Postings: −42.35 on santander, +42.35 on expenses, classified as groceries.
    /// </code>
    /// </example>
    public static JournalEntry RegisterExpense(
        Guid householdId,
        DateOnly occurredOn,
        string description,
        Account paidFrom,
        Account expenseAccount,
        Category category,
        Money amount,
        Guid? memberId = null,
        string? clientMutationId = null,
        Guid? recurringRuleId = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(paidFrom);
        ArgumentNullException.ThrowIfNull(expenseAccount);
        ArgumentNullException.ThrowIfNull(category);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "The amount of an expense is positive. The direction is expressed by the entry, "
                    + "not by the sign the caller passes.",
                nameof(amount));
        }

        EnsureBelongsToHousehold(householdId, paidFrom, expenseAccount, category);

        if (!paidFrom.IsReal)
        {
            throw new InvalidOperationException(
                $"'{paidFrom.Name}' is not an account money can leave: it is nominal.");
        }

        if (expenseAccount.Type != AccountType.Expense)
        {
            throw new InvalidOperationException(
                $"'{expenseAccount.Name}' is not an expense account.");
        }

        if (category.Kind != CategoryKind.Expense)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies income, and this is an expense.");
        }

        if (amount.Currency != paidFrom.Currency)
        {
            throw new InvalidOperationException(
                $"The expense is in {amount.Currency} and '{paidFrom.Name}' works in "
                    + $"{paidFrom.Currency}. Converting it needs an explicit exchange rate.");
        }

        var entry = new JournalEntry(
            Guid.CreateVersion7(),
            householdId,
            EntryKind.Expense,
            occurredOn,
            description.Trim(),
            createdAt ?? DateTimeOffset.UtcNow)
        {
            ClientMutationId = clientMutationId,
            RecurringRuleId = recurringRuleId,
        };

        entry._postings.Add(Posting.Create(entry.Id, paidFrom.Id, -amount, memberId: memberId));
        entry._postings.Add(
            Posting.Create(entry.Id, expenseAccount.Id, amount, category.Id, memberId));

        entry.EnsureBalanced();

        return entry;
    }

    /// <summary>
    /// Verifies invariant I-1: the postings sum to zero in every currency present.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// If the entry has fewer than two postings or does not balance.
    /// </exception>
    /// <remarks>
    /// Called by every factory method before returning. If it throws, the bug is in the factory
    /// method, not in the caller.
    /// </remarks>
    private void EnsureBalanced()
    {
        if (_postings.Count < 2)
        {
            throw new InvalidOperationException(
                "An entry needs at least two postings: money always comes from somewhere.");
        }

        foreach (var group in _postings.GroupBy(p => p.Currency))
        {
            var total = group.Sum(p => p.Amount);

            if (total != 0m)
            {
                throw new InvalidOperationException(
                    $"The entry does not balance: its postings in {group.Key} sum to {total} "
                        + "instead of zero.");
            }
        }
    }

    private static void EnsureBelongsToHousehold(
        Guid householdId,
        Account first,
        Account second,
        Category category)
    {
        if (first.HouseholdId != householdId
            || second.HouseholdId != householdId
            || category.HouseholdId != householdId)
        {
            // In the domain and not only in the queries: otherwise one service forgetting a
            // filter is all it takes to leak another household's data.
            throw new InvalidOperationException(
                "Accounts and categories in an entry must belong to the same household.");
        }
    }
}
