using MyHome.Modules.Shared;

namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// How often a recurring movement repeats.
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>Every month.</summary>
    Monthly = 1,

    /// <summary>Every two months.</summary>
    BiMonthly = 2,

    /// <summary>Every three months.</summary>
    Quarterly = 3,
}

/// <summary>
/// A movement the household knows will happen again: rent, the gym, the vet insurance.
/// </summary>
/// <remarks>
/// A rule is not an entry: nothing hits the balance because a rule exists. Entries are still
/// recorded month by month; the rule only says what to expect. That separation is what will let
/// the projection show a future month without inventing history for it.
/// </remarks>
public sealed class RecurringRule
{
    private RecurringRule(
        Guid id,
        Guid householdId,
        EntryKind kind,
        RecurrenceFrequency frequency,
        Guid accountId,
        Guid categoryId,
        string description,
        decimal amount,
        CurrencyCode currency,
        int dayOfMonth,
        DateOnly startsOn,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        Kind = kind;
        Frequency = frequency;
        AccountId = accountId;
        CategoryId = categoryId;
        Description = description;
        Amount = amount;
        Currency = currency;
        DayOfMonth = dayOfMonth;
        StartsOn = startsOn;
        CreatedAt = createdAt;
    }

    /// <summary>Rule identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Household the rule belongs to.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Whether it repeats an income or an expense.</summary>
    public EntryKind Kind { get; private set; }

    /// <summary>How often it repeats.</summary>
    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>Account the money moves through.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Category it is classified as.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>What it is, reused as the description of the entries it generates.</summary>
    public string Description { get; private set; }

    /// <summary>Expected amount, positive.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency of the amount.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>
    /// Day of the month it is expected on, 1 to 28.
    /// </summary>
    /// <remarks>
    /// Capped at 28 so the day exists in every month. Rules that genuinely fall on the last day
    /// need the negative-day form from the design notes; that comes with the projection.
    /// </remarks>
    public int DayOfMonth { get; private set; }

    /// <summary>First month the rule applies to.</summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>Whether the rule is still in force.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>When the rule was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates a rule out of a movement the user has just recorded.
    /// </summary>
    /// <param name="householdId">Household recording it.</param>
    /// <param name="kind">Income or expense.</param>
    /// <param name="frequency">How often it repeats.</param>
    /// <param name="account">Account the money moves through.</param>
    /// <param name="category">Category it is classified as.</param>
    /// <param name="description">What it is. Required.</param>
    /// <param name="amount">Expected amount. Must be positive.</param>
    /// <param name="firstOccurrence">The occurrence being recorded now.</param>
    /// <param name="createdAt">Creation instant. Defaults to now.</param>
    /// <returns>The new rule.</returns>
    /// <example>
    /// <code>
    /// var rule = RecurringRule.Create(
    ///     householdId,
    ///     EntryKind.Expense,
    ///     RecurrenceFrequency.Monthly,
    ///     santander,
    ///     rent,
    ///     "Alquiler",
    ///     Money.Of(1439m, CurrencyCode.Euro),
    ///     new DateOnly(2026, 8, 1));
    /// </code>
    /// </example>
    public static RecurringRule Create(
        Guid householdId,
        EntryKind kind,
        RecurrenceFrequency frequency,
        Account account,
        Category category,
        string description,
        Money amount,
        DateOnly firstOccurrence,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(category);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException("A rule's amount is positive.", nameof(amount));
        }

        if (account.HouseholdId != householdId || category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "Accounts and categories in a rule must belong to the same household.");
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
            Math.Clamp(firstOccurrence.Day, 1, 28),
            new DateOnly(firstOccurrence.Year, firstOccurrence.Month, 1),
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>Stops the rule without deleting the entries it has already produced.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Whether the rule is expected to fire in the given month.
    /// </summary>
    /// <param name="month">Any day of the month to check.</param>
    /// <returns><see langword="true"/> if that month is one of its occurrences.</returns>
    /// <example>
    /// <code>
    /// // Quarterly rule starting in August: true for August and November, false for September.
    /// rule.OccursIn(new DateOnly(2026, 11, 1));
    /// </code>
    /// </example>
    public bool OccursIn(DateOnly month)
    {
        if (!IsActive)
        {
            return false;
        }

        var elapsed = ((month.Year - StartsOn.Year) * 12) + month.Month - StartsOn.Month;

        return elapsed >= 0 && elapsed % (int)Frequency == 0;
    }
}
