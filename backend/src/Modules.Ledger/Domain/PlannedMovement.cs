using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum PlannedMovementStatus
{
    Pending = 1,
    Settled = 2,
    Skipped = 3,
}

public sealed class PlannedMovement
{
    public const int DefaultDayTolerance = 5;

    private PlannedMovement(
        Guid publicId,
        int householdId,
        int? ruleId,
        int? incomeId,
        EntryKind kind,
        DateOnly dueDate,
        decimal expectedAmount,
        CurrencyCode currency,
        PlannedAmountMode amountMode,
        int accountId,
        int categoryId,
        string description,
        int dayToleranceDays,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        RuleId = ruleId;
        IncomeId = incomeId;
        Kind = kind;
        DueDate = dueDate;
        ExpectedAmount = expectedAmount;
        Currency = currency;
        AmountMode = amountMode;
        AccountId = accountId;
        CategoryId = categoryId;
        Description = description;
        DayToleranceDays = dayToleranceDays;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int HouseholdId { get; private set; }

    public int? RuleId { get; private set; }

    public int? IncomeId { get; private set; }

    public EntryKind Kind { get; private set; }

    public DateOnly DueDate { get; private set; }

    public decimal ExpectedAmount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public PlannedAmountMode AmountMode { get; private set; }

    public int AccountId { get; private set; }

    public int CategoryId { get; private set; }

    public string Description { get; private set; }

    public int DayToleranceDays { get; private set; }

    public PlannedMovementStatus Status { get; private set; } = PlannedMovementStatus.Pending;

    public int? JournalEntryId { get; private set; }

    public JournalEntry? JournalEntry { get; private set; }

    public decimal? ActualAmount { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public decimal? Variance => Kind == EntryKind.Income
        ? ExpectedAmount - ActualAmount
        : ActualAmount - ExpectedAmount;

    public bool IsSettled => Status == PlannedMovementStatus.Settled;

    public static PlannedMovement Create(
        int householdId,
        EntryKind kind,
        Account account,
        Category category,
        string description,
        Money expectedAmount,
        DateOnly dueDate,
        PlannedAmountMode amountMode = PlannedAmountMode.Fixed,
        int dayToleranceDays = DefaultDayTolerance,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(category);

        EnsurePlannable(kind, account, category, expectedAmount, householdId);

        return new PlannedMovement(
            Guid.CreateVersion7(),
            householdId,
            ruleId: null,
            incomeId: null,
            kind,
            dueDate,
            expectedAmount.Amount,
            expectedAmount.Currency,
            amountMode,
            account.Id,
            category.Id,
            description.Trim(),
            Math.Max(0, dayToleranceDays),
            createdAt ?? DateTimeOffset.UtcNow);
    }

    internal static PlannedMovement FromRule(
        RecurringRule rule,
        DateOnly dueDate,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new PlannedMovement(
            Guid.CreateVersion7(),
            rule.HouseholdId,
            rule.Id,
            incomeId: null,
            rule.Kind,
            dueDate,
            rule.Amount,
            rule.Currency,
            rule.AmountMode,
            rule.AccountId,
            rule.CategoryId,
            rule.Description,
            rule.DayToleranceDays,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    internal static PlannedMovement FromIncome(
        Income income,
        DateOnly dueDate,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(income);

        return new PlannedMovement(
            Guid.CreateVersion7(),
            income.HouseholdId,
            ruleId: null,
            income.Id,
            EntryKind.Income,
            dueDate,
            income.Amount,
            income.Currency,
            PlannedAmountMode.Fixed,
            income.AccountId,
            income.CategoryId,
            income.Name,
            income.DayToleranceDays,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public bool IsCandidateFor(int accountId, int categoryId, DateOnly occurredOn) =>
        Status == PlannedMovementStatus.Pending
            && AccountId == accountId
            && CategoryId == categoryId
            && Math.Abs(occurredOn.DayNumber - DueDate.DayNumber) <= DayToleranceDays;

    public bool IsOverdueOn(DateOnly today) =>
        Status == PlannedMovementStatus.Pending
            && today.DayNumber > DueDate.DayNumber + DayToleranceDays;

    public void Settle(JournalEntry entry, Money actual, DateTimeOffset? settledAt = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Status != PlannedMovementStatus.Pending)
        {
            throw new InvalidOperationException(
                Status == PlannedMovementStatus.Settled
                    ? $"'{Description}' has already been settled. Unsettle it first to correct "
                        + "the match."
                    : $"'{Description}' was skipped and is not expecting an entry.");
        }

        if (entry.HouseholdId != HouseholdId)
        {
            throw new InvalidOperationException(
                "An entry cannot settle a movement planned by another household.");
        }

        if (entry.Kind != Kind)
        {
            throw new InvalidOperationException(
                $"'{Description}' expects a {Kind} entry and this one records a {entry.Kind}.");
        }

        if (actual.Amount <= 0m)
        {
            throw new ArgumentException(
                "A settled amount is positive. The direction is the entry's job.",
                nameof(actual));
        }

        if (actual.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"'{Description}' is planned in {Currency} and the entry is in {actual.Currency}. "
                    + "Converting it needs an explicit exchange rate.");
        }

        JournalEntry = entry;
        JournalEntryId = entry.Id;
        ActualAmount = actual.Amount;
        SettledAt = settledAt ?? DateTimeOffset.UtcNow;
        Status = PlannedMovementStatus.Settled;
    }

    public void Unsettle()
    {
        if (Status != PlannedMovementStatus.Settled)
        {
            throw new InvalidOperationException($"'{Description}' is not settled.");
        }

        JournalEntry = null;
        JournalEntryId = null;
        ActualAmount = null;
        SettledAt = null;
        Status = PlannedMovementStatus.Pending;
    }

    public void Skip()
    {
        if (Status == PlannedMovementStatus.Settled)
        {
            throw new InvalidOperationException(
                $"'{Description}' has already happened and cannot be skipped.");
        }

        Status = PlannedMovementStatus.Skipped;
    }

    public void Unskip()
    {
        if (Status != PlannedMovementStatus.Skipped)
        {
            throw new InvalidOperationException($"'{Description}' was not skipped.");
        }

        Status = PlannedMovementStatus.Pending;
    }

    public void Reschedule(DateOnly dueDate)
    {
        EnsureStillOpen("rescheduling it");
        DueDate = dueDate;
    }

    public void Reestimate(Money expectedAmount)
    {
        EnsureStillOpen("re-estimating it");

        if (expectedAmount.Amount <= 0m)
        {
            throw new ArgumentException("An expected amount is positive.", nameof(expectedAmount));
        }

        if (expectedAmount.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"'{Description}' is planned in {Currency} and cannot be re-estimated in "
                    + $"{expectedAmount.Currency}.");
        }

        ExpectedAmount = expectedAmount.Amount;
    }

    private void EnsureStillOpen(string operation)
    {
        if (Status == PlannedMovementStatus.Settled)
        {
            throw new InvalidOperationException(
                $"'{Description}' has already been settled; {operation} would rewrite a variance "
                    + "that has already been reported. Unsettle it first.");
        }
    }

    private static void EnsurePlannable(
        EntryKind kind,
        Account account,
        Category category,
        Money amount,
        int householdId)
    {
        if (kind is not (EntryKind.Income or EntryKind.Expense))
        {
            throw new InvalidOperationException(
                $"Only income and expense are planned; {kind} is not budgeted.");
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "An expected amount is positive. The direction is expressed by the kind, not by "
                    + "the sign the caller passes.",
                nameof(amount));
        }

        if (account.HouseholdId != householdId || category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "The account and the category of a planned movement must belong to the same "
                    + "household.");
        }

        if (!account.IsReal)
        {
            throw new InvalidOperationException(
                $"'{account.Name}' is nominal: no money is ever expected to move through it.");
        }

        var expected = kind == EntryKind.Income ? CategoryKind.Income : CategoryKind.Expense;

        if (category.Kind != expected)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies {category.Kind}, and this plans {kind}.");
        }

        if (amount.Currency != account.Currency)
        {
            throw new InvalidOperationException(
                $"The movement is planned in {amount.Currency} and '{account.Name}' works in "
                    + $"{account.Currency}. Converting it needs an explicit exchange rate.");
        }
    }
}
