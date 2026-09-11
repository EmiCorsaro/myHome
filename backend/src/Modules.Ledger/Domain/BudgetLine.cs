using MyHome.Modules.Shared.Domain;

namespace MyHome.Modules.Ledger.Domain;

/// <summary>
/// Which way the money declared by a budget line moves.
/// </summary>
/// <remarks>
/// A budget line says "this is what I expect to happen", and what can happen is money leaving or
/// money arriving. The sign is kept apart from <see cref="CategoryKind"/> on purpose: the category
/// classifies, the line declares, and the rule that the two must agree is a rule the line enforces
/// rather than a coincidence of types.
/// </remarks>
public enum BudgetLineSign
{
    Expense = 1,
    Income = 2,
}

/// <summary>
/// What a household expects to move in one category during one month.
/// </summary>
/// <remarks>
/// <para>
/// One line per category and month, which is the whole of invariant I-24. A line is a bucket: as
/// many facts as needed are charged against it, and the reading that matters is the sum of what
/// was charged against what was declared.
/// </para>
/// <para>
/// This line carries no calendar. Its amount is therefore spread evenly across every natural day
/// of its month, always starting on the first, and the leftover cents land on the last day. The
/// spread never depends on the day it is asked about, so the budgeted curve of a month is the same
/// line no matter when it is drawn.
/// </para>
/// <para>
/// Income and expense are the same shape: same category, account, amount and mode, same spread,
/// and the difference is the sign and the fact that an income line also says where the money comes
/// from. Declaring either moves no balance: it is an expectation, not a fact.
/// </para>
/// </remarks>
public sealed class BudgetLine : AuditedTenantEntity
{
    private BudgetLine(
        Guid publicId,
        int householdId,
        BudgetLineSign sign,
        int categoryId,
        int accountId,
        DateOnly periodStart,
        decimal amount,
        CurrencyCode currency,
        PlannedAmountMode amountMode,
        BudgetIncomeOrigin? origin,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        HouseholdId = householdId;
        Sign = sign;
        CategoryId = categoryId;
        AccountId = accountId;
        PeriodStart = periodStart;
        Amount = amount;
        Currency = currency;
        AmountMode = amountMode;
        Origin = origin;
        CreatedAt = createdAt;
    }

    /// <summary>Whether the line declares money going out or money coming in.</summary>
    public BudgetLineSign Sign { get; private set; }

    /// <summary>Category the line declares against.</summary>
    public int CategoryId { get; private set; }

    /// <summary>Account the movement is expected against.</summary>
    public int AccountId { get; private set; }

    /// <summary>First day of the month the line covers. Always day 1.</summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>Amount declared for the whole month. Always positive.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency of the line, which is the currency of its account.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>Whether the amount is a fixed figure or an estimate.</summary>
    public PlannedAmountMode AmountMode { get; private set; }

    /// <summary>
    /// Where the expected money comes from. Set on income lines, never on expense ones.
    /// </summary>
    /// <remarks>
    /// It lives beside the category rather than inside it: the category classifies what the money
    /// is for, the origin says what produced it, and neither answer replaces the other (D20).
    /// </remarks>
    public BudgetIncomeOrigin? Origin { get; private set; }

    /// <summary>Last day of the month the line covers, inclusive.</summary>
    public DateOnly PeriodEnd => PeriodStart.AddMonths(1).AddDays(-1);

    /// <summary>Number of natural days the amount is spread across.</summary>
    public int DaysInPeriod => DateTime.DaysInMonth(PeriodStart.Year, PeriodStart.Month);

    /// <summary>
    /// Declares what a household expects to move in one category during one month.
    /// </summary>
    /// <param name="householdId">Household the line belongs to.</param>
    /// <param name="sign">Whether money is expected to leave or to arrive.</param>
    /// <param name="category">Category being declared against, of the same household.</param>
    /// <param name="account">Account the movement is expected against, of the same household.</param>
    /// <param name="month">Any day of the month covered; the period starts on its first day.</param>
    /// <param name="amount">Amount declared for the month, greater than zero.</param>
    /// <param name="amountMode">Whether that amount is fixed or estimated.</param>
    /// <param name="origin">
    /// Where the money comes from. Required when the sign is income, refused when it is expense.
    /// </param>
    /// <param name="createdAt">Instant the line was declared.</param>
    /// <returns>The declared line.</returns>
    /// <exception cref="ArgumentException">
    /// The amount is zero or negative, an income line carries no origin, or an expense line
    /// carries one.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The category or the account is archived, belongs to another household, or the category does
    /// not classify what the line's sign declares.
    /// </exception>
    public static BudgetLine Declare(
        int householdId,
        BudgetLineSign sign,
        Category category,
        Account account,
        DateOnly month,
        decimal amount,
        PlannedAmountMode amountMode = PlannedAmountMode.Estimated,
        BudgetIncomeOrigin? origin = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(account);

        if (amount <= 0m)
        {
            throw new ArgumentException(
                "A budget line declares a positive amount. Declaring zero is declaring nothing.",
                nameof(amount));
        }

        if (category.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "A household cannot budget another household's category.");
        }

        if (category.IsArchived)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' is archived and cannot take a new budget line.");
        }

        if (account.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "A household cannot budget against another household's account.");
        }

        if (account.IsArchived)
        {
            throw new InvalidOperationException(
                $"'{account.Name}' is archived and cannot take a new budget line.");
        }

        if (KindOf(sign) != category.Kind)
        {
            throw new InvalidOperationException(
                $"'{category.Name}' does not classify what this line declares.");
        }

        // Two separate rules, not one: income without an origin is an incomplete declaration,
        // and expense with an origin is a declaration about something an expense does not have.
        if (sign == BudgetLineSign.Income && origin is null)
        {
            throw new ArgumentException(
                "An income budget line declares where the money comes from.",
                nameof(origin));
        }

        if (sign == BudgetLineSign.Expense && origin is not null)
        {
            throw new ArgumentException(
                "An expense budget line has no origin: money going out comes from nowhere.",
                nameof(origin));
        }

        return new BudgetLine(
            Guid.CreateVersion7(),
            householdId,
            sign,
            category.Id,
            account.Id,
            new DateOnly(month.Year, month.Month, 1),

            // The currency is the account's, never the caller's: a line is money expected to move
            // through one account, and an account only moves its own currency.
            Money.Of(amount, account.Currency).Amount,
            account.Currency,
            amountMode,
            origin,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>The whole month's amount, as money.</summary>
    /// <returns>The declared amount in the line's currency.</returns>
    public Money Allowance() => Money.Of(Amount, Currency);

    /// <summary>
    /// What the line does to a balance: positive when money is expected in, negative when out.
    /// </summary>
    /// <returns>The declared amount, signed.</returns>
    /// <remarks>
    /// <see cref="Amount"/> is always positive, because a household declares "300 € of groceries"
    /// and not "minus 300 €". The direction lives in <see cref="Sign"/>, and this is the one place
    /// that turns the two into a single figure, so that whatever draws the budgeted curve adds a
    /// number instead of deciding a direction for itself.
    /// </remarks>
    public decimal SignedAmount() => Sign == BudgetLineSign.Income ? Amount : -Amount;

    /// <summary>
    /// What the line does to a balance on one day of its month.
    /// </summary>
    /// <param name="day">A day inside the line's period.</param>
    /// <returns>The amount imputed to that day, signed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The day falls outside the line's month.
    /// </exception>
    public decimal SignedAmountForDay(DateOnly day) =>
        Sign == BudgetLineSign.Income ? AmountForDay(day) : -AmountForDay(day);

    /// <summary>
    /// The share of the amount that every day of the month carries, except the last.
    /// </summary>
    /// <returns>The daily share, rounded down to the cent.</returns>
    /// <remarks>
    /// Rounded down rather than to nearest so the days never add up to more than was declared. What
    /// is left over is carried by the last day, see <see cref="AmountForDay"/>.
    /// </remarks>
    public decimal DailyAmount() => TruncateToCent(Amount / DaysInPeriod);

    /// <summary>
    /// What the line commits on one day of its month.
    /// </summary>
    /// <param name="day">A day inside the line's period.</param>
    /// <returns>The amount imputed to that day.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The day falls outside the line's month.
    /// </exception>
    /// <remarks>
    /// The answer does not depend on today: a line declared on the 20th commits the same on the 1st
    /// as one declared on the 1st. That is what keeps the budgeted curve of a month from moving
    /// under the household's feet while the month runs.
    /// </remarks>
    public decimal AmountForDay(DateOnly day)
    {
        if (day < PeriodStart || day > PeriodEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                day,
                "That day is not part of the month this line covers.");
        }

        var daily = DailyAmount();

        return day == PeriodEnd
            ? Amount - (daily * (DaysInPeriod - 1))
            : daily;
    }

    /// <summary>
    /// The whole month, day by day.
    /// </summary>
    /// <returns>One entry per natural day of the month, in order, adding up to the amount.</returns>
    public IReadOnlyList<(DateOnly Day, decimal Amount)> Spread()
    {
        var days = new (DateOnly Day, decimal Amount)[DaysInPeriod];

        for (var offset = 0; offset < days.Length; offset++)
        {
            var day = PeriodStart.AddDays(offset);
            days[offset] = (day, AmountForDay(day));
        }

        return days;
    }

    /// <summary>The kind of category a sign can be declared against.</summary>
    /// <param name="sign">Sign of the line.</param>
    /// <returns>The only category kind that matches it.</returns>
    public static CategoryKind KindOf(BudgetLineSign sign) => sign switch
    {
        BudgetLineSign.Expense => CategoryKind.Expense,
        BudgetLineSign.Income => CategoryKind.Income,
        _ => throw new ArgumentOutOfRangeException(nameof(sign), sign, "Unknown budget line sign."),
    };

    private static decimal TruncateToCent(decimal value) => decimal.Floor(value * 100m) / 100m;
}
