using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum EntryKind
{
    Income = 1,
    Expense = 2,
    Transfer = 3,
    Opening = 4,
}

public sealed class JournalEntry
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

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int HouseholdId { get; private set; }

    public EntryKind Kind { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? ClientMutationId { get; private set; }

    public int? RecurringRuleId { get; private set; }

    public RecurringRule? RecurringRule { get; private set; }

    public IReadOnlyList<Posting> Postings => _postings;

    public static JournalEntry RegisterExpense(
        int householdId,
        DateOnly occurredOn,
        string description,
        Account paidFrom,
        Account expenseAccount,
        Category category,
        Money amount,
        int? memberId = null,
        string? clientMutationId = null,
        RecurringRule? recurringRule = null,
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
            RecurringRule = recurringRule,
        };

        entry._postings.Add(Posting.Create(paidFrom.Id, -amount, memberId: memberId));
        entry._postings.Add(
            Posting.Create(expenseAccount.Id, amount, category.Id, memberId));

        entry.EnsureBalanced();

        return entry;
    }

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
