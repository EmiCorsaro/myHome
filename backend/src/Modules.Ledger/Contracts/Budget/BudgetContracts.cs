namespace MyHome.Modules.Ledger.Contracts.Budget;

/// <summary>
/// What the household declares it expects to move in one category during one month.
/// </summary>
/// <param name="CategoryId">Category the line is declared against.</param>
/// <param name="AccountId">Account the movement is expected against. Sets the line's currency.</param>
/// <param name="Month">Any day of the month covered; the period is the whole natural month.</param>
/// <param name="Amount">Amount expected for the month, greater than zero.</param>
/// <param name="AmountMode"><c>fixed</c> or <c>estimated</c>.</param>
/// <param name="Sign">
/// <c>expense</c> or <c>income</c>. Defaults to <c>expense</c>, which is what story 051 was able
/// to declare, so a caller written against that contract keeps working word for word.
/// </param>
/// <param name="Origin">
/// Where the money comes from, one of <see cref="BudgetIncomeOrigins"/>. Required on an income
/// line and refused on an expense one.
/// </param>
public sealed record DeclareBudgetLineRequest(
    Guid CategoryId,
    Guid AccountId,
    DateOnly Month,
    decimal Amount,
    string AmountMode = BudgetAmountModes.Estimated,
    string Sign = BudgetLineSigns.Expense,
    string? Origin = null);

/// <summary>Which way the money a budget line declares moves, as published.</summary>
public static class BudgetLineSigns
{
    /// <summary>Money the household expects to pay out.</summary>
    public const string Expense = "expense";

    /// <summary>Money the household expects to receive.</summary>
    public const string Income = "income";
}

/// <summary>
/// Where the money of an income budget line comes from, as published.
/// </summary>
/// <remarks>
/// A closed list: the household neither extends it nor renames it. What fits none of the other
/// seven is declared as <see cref="Other"/>.
/// </remarks>
public static class BudgetIncomeOrigins
{
    /// <summary>Wages from employment.</summary>
    public const string Payroll = "payroll";

    /// <summary>Billing from one's own trade or business.</summary>
    public const string SelfEmployment = "selfEmployment";

    /// <summary>Rent collected from a property.</summary>
    public const string Rental = "rental";

    /// <summary>Returns on capital.</summary>
    public const string Investment = "investment";

    /// <summary>A public allowance or subsidy.</summary>
    public const string Benefit = "benefit";

    /// <summary>Money coming back, such as a tax refund.</summary>
    public const string Refund = "refund";

    /// <summary>Money given, with nothing expected back.</summary>
    public const string Gift = "gift";

    /// <summary>Anything the other seven do not cover.</summary>
    public const string Other = "other";

    /// <summary>Every origin a household may declare, in the order the list is read.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Payroll,
        SelfEmployment,
        Rental,
        Investment,
        Benefit,
        Refund,
        Gift,
        Other,
    ];
}

/// <summary>The two ways a budget line's amount can be read, as published.</summary>
public static class BudgetAmountModes
{
    /// <summary>A figure known in advance: the rent, the season ticket.</summary>
    public const string Fixed = "fixed";

    /// <summary>A figure that is a guess: the supermarket, the fuel.</summary>
    public const string Estimated = "estimated";
}

/// <summary>One budget line as published.</summary>
/// <param name="Id">Line identifier.</param>
/// <param name="Sign"><c>expense</c> or <c>income</c>.</param>
/// <param name="CategoryId">Category the line is declared against.</param>
/// <param name="CategoryName">That category's visible name.</param>
/// <param name="CategoryColorIndex">That category's tone, 1 to 10.</param>
/// <param name="AccountId">Account the movement is expected against.</param>
/// <param name="AccountName">That account's visible name.</param>
/// <param name="PeriodStart">First day of the month covered.</param>
/// <param name="PeriodEnd">Last day of the month covered, inclusive.</param>
/// <param name="Amount">Amount expected for the whole month.</param>
/// <param name="Currency">Three-letter ISO 4217 code, taken from the account.</param>
/// <param name="AmountMode"><c>fixed</c> or <c>estimated</c>.</param>
/// <param name="DailyAmount">
/// What each day of the month carries, the last one aside. The line has no calendar, so its amount
/// is spread evenly from day 1 and the leftover cents land on the last day.
/// </param>
/// <param name="Origin">
/// Where the money comes from on an income line, one of <see cref="BudgetIncomeOrigins"/>.
/// <c>null</c> on an expense line.
/// </param>
/// <param name="SignedAmount">
/// The month's amount as it weighs on a balance: positive on an income line, negative on an
/// expense one. Published so nobody has to re-derive the direction from the sign.
/// </param>
public sealed record BudgetLineSummary(
    Guid Id,
    string Sign,
    Guid CategoryId,
    string CategoryName,
    int CategoryColorIndex,
    Guid AccountId,
    string AccountName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Currency,
    string AmountMode,
    decimal DailyAmount,
    string? Origin,
    decimal SignedAmount);

/// <summary>The month's expected income, split by where it comes from.</summary>
/// <param name="Origin">One of <see cref="BudgetIncomeOrigins"/>.</param>
/// <param name="Amount">Sum of the income lines of the month declaring that origin.</param>
public sealed record BudgetIncomeOriginTotal(string Origin, decimal Amount);

/// <summary>Everything the budget screen shows for one month.</summary>
/// <param name="PeriodStart">First day of the month.</param>
/// <param name="PeriodEnd">Last day of the month, inclusive.</param>
/// <param name="Currency">The household's base currency.</param>
/// <param name="CommittedTotal">
/// Sum of the amounts of the month's <em>expense</em> lines. Zero when none has been declared,
/// which is not an error.
/// </param>
/// <param name="Lines">The month's lines, by category name.</param>
/// <param name="ExpectedIncomeTotal">
/// Sum of the amounts of the month's <em>income</em> lines. Kept apart from
/// <paramref name="CommittedTotal"/> rather than netted against it: money committed and money
/// expected are two different readings and a single net figure hides both.
/// </param>
/// <param name="IncomeByOrigin">
/// <paramref name="ExpectedIncomeTotal"/> broken down by origin, in the order the closed list is
/// read, and carrying only the origins the month actually declares.
/// </param>
public sealed record MonthBudget(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    decimal CommittedTotal,
    IReadOnlyList<BudgetLineSummary> Lines,
    decimal ExpectedIncomeTotal,
    IReadOnlyList<BudgetIncomeOriginTotal> IncomeByOrigin);
