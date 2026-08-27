using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

public enum IncomeSource
{
    Salary = 1,
    SelfEmployment = 2,
    Bonus = 3,
    Rental = 4,
    Investment = 5,
    Benefit = 6,
    Refund = 7,
    Gift = 8,
    Other = 9,
}

public enum IncomePeriodicity
{
    OneOff = 0,
    Monthly = 1,
    Semiannual = 6,
}

public sealed class Income
{
    private Income(
        Guid publicId,
        int householdId,
        string name,
        IncomeSource source,
        IncomePeriodicity periodicity,
        int accountId,
        int categoryId,
        decimal amount,
        CurrencyCode currency,
        int dayOfMonth,
        int dayToleranceDays,
        DateOnly startsOn,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Name = name;
        Source = source;
        Periodicity = periodicity;
        AccountId = accountId;
        CategoryId = categoryId;
        Amount = amount;
        Currency = currency;
        DayOfMonth = dayOfMonth;
        DayToleranceDays = dayToleranceDays;
        StartsOn = startsOn;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int HouseholdId { get; private set; }

    public string Name { get; private set; }

    public IncomeSource Source { get; private set; }

    public IncomePeriodicity Periodicity { get; private set; }

    public int AccountId { get; private set; }

    public int CategoryId { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public int DayOfMonth { get; private set; }

    public int DayToleranceDays { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsRecurring => Periodicity != IncomePeriodicity.OneOff;

    public static Income Create(
        int householdId,
        string name,
        IncomeSource source,
        IncomePeriodicity periodicity,
        Account account,
        Category category,
        Money amount,
        DateOnly startsOn,
        int dayToleranceDays = PlannedMovement.DefaultDayTolerance,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(category);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException(
                "An income's amount is positive. The direction is expressed by the entries it "
                    + "produces, not by the sign the caller passes.",
                nameof(amount));
        }

        if (account.HouseholdId != householdId || category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "The account and the category of an income must belong to the same household.");
        }

        if (!account.IsReal)
        {
            throw new InvalidOperationException(
                $"'{account.Name}' is not an account money can land in: it is nominal.");
        }

        if (category.Kind != CategoryKind.Income)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' classifies expense, and this is an income.");
        }

        if (amount.Currency != account.Currency)
        {
            throw new InvalidOperationException(
                $"The income is in {amount.Currency} and '{account.Name}' works in "
                    + $"{account.Currency}. Converting it needs an explicit exchange rate.");
        }

        return new Income(
            Guid.CreateVersion7(),
            householdId,
            name.Trim(),
            source,
            periodicity,
            account.Id,
            category.Id,
            amount.Amount,
            amount.Currency,
            Math.Clamp(startsOn.Day, 1, 28),
            Math.Max(0, dayToleranceDays),
            startsOn,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public Money ExpectedAmount() => Money.Of(Amount, Currency);

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;

    public bool OccursIn(DateOnly month)
    {
        if (!IsActive)
        {
            return false;
        }

        var elapsed = ((month.Year - StartsOn.Year) * 12) + month.Month - StartsOn.Month;

        if (elapsed < 0)
        {
            return false;
        }

        return IsRecurring ? elapsed % (int)Periodicity == 0 : elapsed == 0;
    }

    public IEnumerable<DateOnly> OccurrencesBetween(DateOnly from, DateOnly to)
    {
        if (!IsActive || from > to)
        {
            yield break;
        }

        if (!IsRecurring)
        {
            if (StartsOn >= from && StartsOn <= to)
            {
                yield return StartsOn;
            }

            yield break;
        }

        var interval = (int)Periodicity;
        var elapsed = ((from.Year - StartsOn.Year) * 12) + from.Month - StartsOn.Month;

        var skipped = elapsed <= 0 ? 0 : elapsed / interval * interval;
        var first = new DateOnly(StartsOn.Year, StartsOn.Month, 1);

        for (var month = first.AddMonths(skipped); ; month = month.AddMonths(interval))
        {
            var due = new DateOnly(month.Year, month.Month, DayOfMonth);

            if (due > to)
            {
                yield break;
            }

            if (due >= from)
            {
                yield return due;
            }
        }
    }
}
