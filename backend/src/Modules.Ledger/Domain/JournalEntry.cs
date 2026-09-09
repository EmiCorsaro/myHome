using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum EntryKind
{
    Income = 1,
    Expense = 2,
    Transfer = 3,
    Opening = 4,
}

public sealed class JournalEntry : AuditedTenantEntity
{
    private readonly List<Posting> _postings = [];

    private JournalEntry(
        Guid publicId,
        int householdId,
        EntryKind kind,
        DateOnly occurredOn,
        string description,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Kind = kind;
        OccurredOn = occurredOn;
        Description = description;
        CreatedAt = createdAt;
    }

    public EntryKind Kind { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    public string Description { get; private set; }

    public string? ClientMutationId { get; private set; }

    public int? RecurringRuleId { get; private set; }

    /// <summary>
    /// The account this entry states the opening balance of, when it is an opening entry.
    /// </summary>
    /// <remarks>
    /// Null for every other kind. It is what makes "one opening balance per account" a rule the
    /// database can hold on its own, and what lets the ledger find an account's opening balance
    /// without walking its postings.
    /// </remarks>
    public int? OpeningAccountId { get; private set; }

    /// <summary>
    /// Marks this entry as reversed (story 013, RF-3).
    /// </summary>
    /// <remarks>
    /// The entry itself is never deleted (RF-9, RF-2): this is the only trace that it was undone.
    /// </remarks>
    public bool IsVoided { get; private set; }

    /// <summary>
    /// The entry this one reverses, when this entry is itself a reversal (story 013, RF-1, RF-6).
    /// </summary>
    /// <remarks>
    /// Null for every ordinary entry. Together with <see cref="IsVoided"/> on the entry it points
    /// at, the link is visible from either side without walking the ledger by date or amount
    /// (RF-6): from the original, a reversal is found by its id; from the reversal, the original is
    /// this very field.
    /// </remarks>
    public int? ReversalOfEntryId { get; private set; }

    public RecurringRule? RecurringRule { get; private set; }

    public IReadOnlyList<Posting> Postings => _postings;

    public static JournalEntry RegisterExpense(
        int householdId,
        DateOnly occurredOn,
        string? description,
        Account paidFrom,
        Account expenseAccount,
        Category category,
        Money amount,
        int? memberId = null,
        string? clientMutationId = null,
        RecurringRule? recurringRule = null,
        DateTimeOffset? createdAt = null)
    {
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
            description?.Trim() ?? string.Empty,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            ClientMutationId = clientMutationId,
            RecurringRule = recurringRule,
        };

        entry._postings.Add(Posting.Create(paidFrom.Id, -amount, memberId: memberId));
        entry._postings.Add(
            Posting.Create(expenseAccount.Id, amount, category.Id, memberId));

        entry.EnsureBalanced();

        return entry;
    }

    /// <summary>
    /// Records money coming into the household (story 010, RF-1, RF-2, RF-3).
    /// </summary>
    /// <param name="householdId">Household the income belongs to.</param>
    /// <param name="occurredOn">Date the money was received.</param>
    /// <param name="description">Free text, kept empty rather than null (RF-4).</param>
    /// <param name="depositTo">The real account the money lands on.</param>
    /// <param name="incomeAccount">The nominal account the money leaves, one per currency.</param>
    /// <param name="category">The income category classifying the entry.</param>
    /// <param name="amount">How much was received; always positive (the entry expresses direction).</param>
    /// <param name="memberId">The member who perceived the income, when one is named (RF-14, RF-15).</param>
    /// <param name="clientMutationId">Idempotency key for the request that asked for this (RF-12).</param>
    /// <param name="createdAt">When the entry was recorded; now, unless a test says otherwise.</param>
    /// <returns>The income entry, ready to be saved.</returns>
    /// <remarks>
    /// This is the mirror image of <see cref="RegisterExpense"/>: the money enters the real account
    /// and leaves the nominal one, instead of the other way around. A credit card is refused as the
    /// depositing account (RF-13): a credit to it is not income, it is the correction of the expense
    /// that put the debt there in the first place, and that correction belongs to story 013.
    /// </remarks>
    public static JournalEntry RegisterIncome(
        int householdId,
        DateOnly occurredOn,
        string? description,
        Account depositTo,
        Account incomeAccount,
        Category category,
        Money amount,
        int? memberId = null,
        string? clientMutationId = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(depositTo);
        ArgumentNullException.ThrowIfNull(incomeAccount);
        ArgumentNullException.ThrowIfNull(category);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "The amount of an income is positive. The direction is expressed by the entry, "
                    + "not by the sign the caller passes.",
                nameof(amount));
        }

        EnsureBelongsToHousehold(householdId, depositTo, incomeAccount, category);

        if (!depositTo.IsReal)
        {
            throw new InvalidOperationException(
                $"'{depositTo.Name}' is not an account money can enter: it is nominal.");
        }

        if (depositTo.Type == AccountType.CreditCard)
        {
            throw new InvalidOperationException(
                $"A credit to '{depositTo.Name}' is not an income: it is the correction of the "
                    + "expense that caused it.");
        }

        if (incomeAccount.Type != AccountType.Income)
        {
            throw new InvalidOperationException(
                $"'{incomeAccount.Name}' is not an income account.");
        }

        if (category.Kind != CategoryKind.Income)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies an expense, and this is an income.");
        }

        if (amount.Currency != depositTo.Currency)
        {
            throw new InvalidOperationException(
                $"The income is in {amount.Currency} and '{depositTo.Name}' works in "
                    + $"{depositTo.Currency}. Converting it needs an explicit exchange rate.");
        }

        var entry = new JournalEntry(
            Guid.CreateVersion7(),
            householdId,
            EntryKind.Income,
            occurredOn,
            description?.Trim() ?? string.Empty,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            ClientMutationId = clientMutationId,
        };

        entry._postings.Add(Posting.Create(depositTo.Id, amount, memberId: memberId));
        entry._postings.Add(Posting.Create(incomeAccount.Id, -amount, category.Id, memberId));

        entry.EnsureBalanced();

        return entry;
    }

    /// <summary>
    /// Moves money between two of the household's own accounts (story 011, RF-1 through RF-6).
    /// </summary>
    /// <param name="householdId">Household both accounts belong to.</param>
    /// <param name="occurredOn">Date the money moved.</param>
    /// <param name="description">Free text, kept empty rather than null (RF-7).</param>
    /// <param name="from">The account the money leaves; never a credit card (RF-19).</param>
    /// <param name="to">The account the money lands on.</param>
    /// <param name="amount">How much moved; always positive (the entry expresses direction).</param>
    /// <param name="category">
    /// The expense category classifying the transfer. Required, and attached to the destination's
    /// own posting, only when <paramref name="to"/> is not controlled (RF-14, RF-16): that is the
    /// household's only chance to classify money leaving the tracked perimeter. Ignored otherwise
    /// (RF-4): a transfer between two controlled accounts carries no category at all, whatever the
    /// caller passes.
    /// </param>
    /// <param name="clientMutationId">Idempotency key for the request that asked for this (RF-13).</param>
    /// <param name="createdAt">When the entry was recorded; now, unless a test says otherwise.</param>
    /// <returns>The transfer entry, ready to be saved.</returns>
    /// <remarks>
    /// Between two controlled accounts this is two real postings and nothing else: no nominal leg,
    /// so it never surfaces as income or expense of any period (RF-2, RF-3, RF-4). When the
    /// destination is not controlled, the category rides on the destination's own posting instead
    /// of a nominal account, which is what lets the spend-by-category report pick it up through the
    /// same "any posting that carries a category" rule an expense already relies on, without the
    /// report needing to know a transfer even exists (RF-16).
    /// </remarks>
    public static JournalEntry RegisterTransfer(
        int householdId,
        DateOnly occurredOn,
        string? description,
        Account from,
        Account to,
        Money amount,
        Category? category = null,
        string? clientMutationId = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "The amount of a transfer is positive. The direction is expressed by the entry, "
                    + "not by the sign the caller passes.",
                nameof(amount));
        }

        if (from.HouseholdId != householdId
            || to.HouseholdId != householdId
            || (category is not null && category.HouseholdId != householdId))
        {
            throw new InvalidOperationException(
                "Accounts and categories in an entry must belong to the same household.");
        }

        if (from.Id == to.Id)
        {
            throw new InvalidOperationException(
                "A transfer needs two different accounts: money cannot move from an account to "
                    + "itself.");
        }

        if (!from.IsReal || !to.IsReal)
        {
            throw new InvalidOperationException(
                "A transfer moves money between real accounts; a nominal one holds none of its "
                    + "own.");
        }

        if (from.Type == AccountType.CreditCard)
        {
            throw new InvalidOperationException(
                $"'{from.Name}' is a credit card: a cash advance is not admitted yet, it accrues "
                    + "its own interest from day one.");
        }

        if (amount.Currency != from.Currency || amount.Currency != to.Currency)
        {
            throw new InvalidOperationException(
                $"The transfer is in {amount.Currency}, but '{from.Name}' and '{to.Name}' do not "
                    + "both work in it. Converting between currencies needs an explicit exchange "
                    + "rate.");
        }

        if (category is not null && category.Kind != CategoryKind.Expense)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies income, and money leaving the tracked accounts is "
                    + "always spent, never earned.");
        }

        var requiresCategory = !to.IsTracked;

        if (requiresCategory && category is null)
        {
            throw new InvalidOperationException(
                $"'{to.Name}' is not controlled: money moving there is the household's only "
                    + "chance to classify it, so a category is required.");
        }

        var entry = new JournalEntry(
            Guid.CreateVersion7(),
            householdId,
            EntryKind.Transfer,
            occurredOn,
            description?.Trim() ?? string.Empty,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            ClientMutationId = clientMutationId,
        };

        entry._postings.Add(Posting.Create(from.Id, -amount));
        entry._postings.Add(
            Posting.Create(to.Id, amount, requiresCategory ? category!.Id : null));

        entry.EnsureBalanced();

        return entry;
    }

    /// <summary>
    /// States with how much money an account starts (RF-1).
    /// </summary>
    /// <param name="householdId">Household the account belongs to.</param>
    /// <param name="account">Account whose starting position is being declared.</param>
    /// <param name="amount">
    /// The money the account holds at that date. Negative means debt, which is what a credit card
    /// starts with (RF-4); zero is a statement like any other (RF-5).
    /// </param>
    /// <param name="occurredOn">Date the balance is stated at.</param>
    /// <param name="createdAt">When the entry was recorded; now, unless a test says otherwise.</param>
    /// <returns>The opening entry, ready to be saved.</returns>
    /// <remarks>
    /// Unlike every other kind, an opening entry carries a single posting. The counterpart of an
    /// opening balance is equity, and this phase keeps no equity account (see
    /// <c>docs/06-scope-review.md</c>): inventing one would put a row the household cannot see, and
    /// must never be told about, in the middle of its account list. The posting carries no category
    /// on purpose, which is what keeps the opening balance out of the spend-by-category report
    /// (RF-3), and it lands on a real account rather than a nominal one, which is what keeps it out
    /// of the income and expense of any period (RF-2).
    /// </remarks>
    public static JournalEntry OpenBalance(
        int householdId,
        Account account,
        Money amount,
        DateOnly occurredOn,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "The account whose opening balance is stated must belong to the household.");
        }

        if (!account.IsReal)
        {
            throw new InvalidOperationException(
                $"'{account.Name}' holds no money of its own: it is nominal.");
        }

        if (amount.Currency != account.Currency)
        {
            throw new InvalidOperationException(
                $"The opening balance is in {amount.Currency} and '{account.Name}' works in "
                    + $"{account.Currency}.");
        }

        var entry = new JournalEntry(
            Guid.CreateVersion7(),
            householdId,
            EntryKind.Opening,
            occurredOn,
            OpeningDescription,
            createdAt ?? DateTimeOffset.UtcNow)
        {
            OpeningAccountId = account.Id,
        };

        entry._postings.Add(Posting.Create(account.Id, amount));

        return entry;
    }

    /// <summary>
    /// Corrects an opening balance already declared (RF-9).
    /// </summary>
    /// <param name="amount">The amount the account really started with.</param>
    /// <param name="occurredOn">The date it started at.</param>
    /// <remarks>
    /// The entry is restated rather than replaced, so the account's balance is recomputed from the
    /// new figure without touching a single movement recorded since (RF-10).
    /// </remarks>
    public void AmendOpeningBalance(Money amount, DateOnly occurredOn)
    {
        if (Kind != EntryKind.Opening)
        {
            throw new InvalidOperationException(
                "Only an opening balance is amended this way; any other entry is corrected as the "
                    + "movement it is.");
        }

        var posting = _postings.Single();

        if (amount.Currency != posting.Currency)
        {
            throw new InvalidOperationException(
                $"The opening balance is kept in {posting.Currency} and cannot be restated in "
                    + $"{amount.Currency}.");
        }

        posting.Restate(amount);
        OccurredOn = occurredOn;
    }

    /// <summary>
    /// Reverses this movement, leaving the balances it touched exactly as they were before it
    /// (story 013, RF-1, RF-2, RF-3, RF-11).
    /// </summary>
    /// <param name="createdAt">When the reversal is recorded; now, unless a test says otherwise.</param>
    /// <returns>The reversal entry, ready to be saved alongside this one, now marked as voided.</returns>
    /// <remarks>
    /// The reversal is built by negating every posting this entry carries, whatever shape the entry
    /// has: an expense, an income and a transfer each carry a different pair of postings, and
    /// negating them all, rather than assuming a fixed shape, is what lets one method reverse the
    /// three (RF-10) without repeating their logic. It keeps this entry's date (RF-11): moving it to
    /// today would leave every day between the two looking like the movement still happened, which
    /// is exactly what the household's reconciliation cannot afford. This entry is never deleted
    /// (RF-2, RF-9); only marked, so a second reversal is never mistaken for the first.
    /// <para/>
    /// An opening balance is not reversed: it states a position, not something that happened, and
    /// story 003 already gave it its own way to be corrected (RF-12). A reversal itself is refused
    /// too (RF-8): it is the end of the correction chain, not one more movement that can be undone.
    /// So is a movement already voided (RF-7): only one reversal ever exists for it, which is also
    /// what the unique index on <see cref="ReversalOfEntryId"/> holds the database to.
    /// </remarks>
    public JournalEntry Reverse(DateTimeOffset createdAt)
    {
        if (Kind == EntryKind.Opening)
        {
            throw new InvalidOperationException(
                "An opening balance is not voided: it is corrected by editing it.");
        }

        if (ReversalOfEntryId is not null)
        {
            throw new InvalidOperationException(
                "A reversal cannot itself be voided: it is already the end of the correction "
                    + "chain.");
        }

        if (IsVoided)
        {
            throw new InvalidOperationException("This movement has already been voided.");
        }

        var reversal = new JournalEntry(
            Guid.CreateVersion7(),
            HouseholdId,
            Kind,
            OccurredOn,
            ReversalDescriptionFor(Description),
            createdAt)
        {
            ReversalOfEntryId = Id,
        };

        foreach (var posting in _postings)
        {
            reversal._postings.Add(Posting.Create(
                posting.AccountId,
                Money.Of(-posting.Amount, posting.Currency),
                posting.CategoryId,
                posting.MemberId));
        }

        reversal.EnsureBalanced();

        IsVoided = true;

        return reversal;
    }

    private static string ReversalDescriptionFor(string description) =>
        string.IsNullOrEmpty(description) ? "Void" : $"Void of: {description}";

    /// <summary>What an opening balance is called wherever entries are listed.</summary>
    public const string OpeningDescription = "Saldo inicial";

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
        int householdId,
        Account first,
        Account second,
        Category category)
    {
        if (first.HouseholdId != householdId
            || second.HouseholdId != householdId
            || category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "Accounts and categories in an entry must belong to the same household.");
        }
    }
}
