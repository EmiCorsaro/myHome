using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum RecurrenceFrequency
{
    Monthly = 1,
    BiMonthly = 2,
    Quarterly = 3,
}

public sealed class RecurringRule
{
    private RecurringRule(
        Guid publicId,
        int householdId,
        EntryKind kind,
        RecurrenceFrequency frequency,
        int accountId,
        int categoryId,
        string description,
        decimal amount,
        CurrencyCode currency,
        PlannedAmountMode amountMode,
        int dayOfMonth,
        int dayToleranceDays,
        DateOnly startsOn,
        DateOnly? endsOn,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Kind = kind;
        Frequency = frequency;
        AccountId = accountId;
        CategoryId = categoryId;
        Description = description;
        Amount = amount;
        Currency = currency;
        AmountMode = amountMode;
        DayOfMonth = dayOfMonth;
        DayToleranceDays = dayToleranceDays;
        StartsOn = startsOn;
        EndsOn = endsOn;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int HouseholdId { get; private set; }

    public EntryKind Kind { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    public int AccountId { get; private set; }

    public int CategoryId { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public PlannedAmountMode AmountMode { get; private set; }

    public int DayOfMonth { get; private set; }

    public int DayToleranceDays { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly? EndsOn { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public static RecurringRule Create(
        int householdId,
        EntryKind kind,
        RecurrenceFrequency frequency,
        Account account,
        Category category,
        string description,
        Money amount,
        DateOnly firstDueDate,
        PlannedAmountMode amountMode = PlannedAmountMode.Fixed,
        int dayToleranceDays = PlannedMovement.DefaultDayTolerance,
        DateOnly? endsOn = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(category);

        if (kind is not (EntryKind.Income or EntryKind.Expense))
        {
            throw new InvalidOperationException(
                $"Only income and expense repeat on a schedule; {kind} does not.");
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "A rule's amount is positive. The direction is expressed by its kind, not by the "
                    + "sign the caller passes.",
                nameof(amount));
        }

        if (account.HouseholdId != householdId || category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "Accounts and categories in a rule must belong to the same household.");
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
                $"'{category.Name}' classifies {category.Kind}, and this rule repeats {kind}.");
        }

        if (amount.Currency != account.Currency)
        {
            throw new InvalidOperationException(
                $"The rule is in {amount.Currency} and '{account.Name}' works in "
                    + $"{account.Currency}. Converting it needs an explicit exchange rate.");
        }

        if (endsOn is { } end && end < firstDueDate)
        {
            throw new InvalidOperationException(
                "A rule cannot end before its first occurrence.");
        }

        return new RecurringRule(
            Guid.CreateVersion7(),
            householdId,
            kind,
            frequency,
            account.Id,
            category.Id,
            description.Trim(),
            amount.Amount,
            amount.Currency,
            amountMode,
            Math.Clamp(firstDueDate.Day, 1, 28),
            Math.Max(0, dayToleranceDays),
            new DateOnly(firstDueDate.Year, firstDueDate.Month, 1),
            endsOn,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public Money ExpectedAmount() => Money.Of(Amount, Currency);

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void Amend(Money amount, int? dayOfMonth = null, PlannedAmountMode? amountMode = null)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentException("A rule's amount is positive.", nameof(amount));
        }

        if (amount.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"The rule is declared in {Currency} and cannot be amended in {amount.Currency}.");
        }

        Amount = amount.Amount;

        if (dayOfMonth is { } day)
        {
            DayOfMonth = Math.Clamp(day, 1, 28);
        }

        if (amountMode is { } mode)
        {
            AmountMode = mode;
        }
    }

    public void ScheduleEnd(DateOnly? endsOn)
    {
        if (endsOn is { } end && end < StartsOn)
        {
            throw new InvalidOperationException("A rule cannot end before it starts.");
        }

        EndsOn = endsOn;
    }

    public bool OccursIn(DateOnly month)
    {
        if (!IsActive)
        {
            return false;
        }

        var elapsed = ((month.Year - StartsOn.Year) * 12) + month.Month - StartsOn.Month;

        if (elapsed < 0 || elapsed % (int)Frequency != 0)
        {
            return false;
        }

        return EndsOn is not { } endsOn || DueDateIn(month) <= endsOn;
    }

    public IEnumerable<DateOnly> OccurrencesBetween(DateOnly from, DateOnly to)
    {
        if (!IsActive || from > to)
        {
            yield break;
        }

        var interval = (int)Frequency;
        var elapsed = ((from.Year - StartsOn.Year) * 12) + from.Month - StartsOn.Month;

        var skipped = elapsed <= 0 ? 0 : elapsed / interval * interval;

        for (var month = StartsOn.AddMonths(skipped); ; month = month.AddMonths(interval))
        {
            var due = DueDateIn(month);

            if (due > to || (EndsOn is { } endsOn && due > endsOn))
            {
                yield break;
            }

            if (due >= from)
            {
                yield return due;
            }
        }
    }

    private DateOnly DueDateIn(DateOnly month) => new(month.Year, month.Month, DayOfMonth);
}
