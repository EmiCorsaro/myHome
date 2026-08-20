using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Contracts.Dashboard;

/// <summary>
/// Everything the landing screen shows, in a single response.
/// </summary>
/// <remarks>
/// One call, not five. A dashboard assembled from five requests renders in five stages, each with
/// its own spinner and its own chance to fail, and it invites the client to start totalling
/// partial data. The frontend formats and lays out; it does not add anything up.
/// </remarks>
/// <param name="Currency">The household's base currency, three-letter ISO 4217.</param>
/// <param name="PeriodStart">First day of the period the totals cover.</param>
/// <param name="PeriodEnd">Last day of the period, inclusive.</param>
/// <param name="Income">Money that came in during the period. Positive.</param>
/// <param name="Expense">Money that went out during the period. Positive.</param>
/// <param name="Net">
/// <paramref name="Income"/> minus <paramref name="Expense"/>. Negative means the household
/// spent more than it earned this period.
/// </param>
/// <param name="TrackedBalance">
/// Money available right now across tracked accounts. Not a period figure: it is today's
/// balance, which is what "can we cover the direct debits" is really asking.
/// </param>
/// <param name="IsProjectionAvailable">
/// Whether a forward-looking projection can be computed. <see langword="false"/> until there are
/// recurring rules to project from (sub-phase 1.2). The screen says so rather than extrapolating
/// one month of history, which would look like an answer without being one.
/// </param>
/// <param name="ByCategory">
/// Expense broken down by category, largest first. Empty when nothing was recorded.
/// </param>
/// <param name="Accounts">Accounts holding real money, with their current balances.</param>
/// <param name="RecentEntries">The period's movements, newest first.</param>
public sealed record DashboardSummary(
    string Currency,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Income,
    decimal Expense,
    decimal Net,
    decimal TrackedBalance,
    bool IsProjectionAvailable,
    IReadOnlyList<CategoryTotal> ByCategory,
    IReadOnlyList<AccountSummary> Accounts,
    IReadOnlyList<LedgerEntrySummary> RecentEntries);

/// <summary>
/// What was spent on one category during the period.
/// </summary>
/// <param name="CategoryId">Category identifier.</param>
/// <param name="Name">Visible name.</param>
/// <param name="ColorIndex">Tone from the expressive palette, 1 to 10.</param>
/// <param name="Total">Amount spent, positive.</param>
/// <param name="Share">
/// Fraction of the period's total expense, from 0 to 1. Computed here so every client rounds it
/// the same way.
/// </param>
public sealed record CategoryTotal(
    Guid CategoryId,
    string Name,
    int ColorIndex,
    decimal Total,
    decimal Share);

/// <summary>
/// One entry, flattened for a listing.
/// </summary>
/// <remarks>
/// The cash view of an entry: one line, one amount, signed from the household's point of view.
/// The postings are still underneath; nobody needs them to read a list of movements.
/// </remarks>
/// <param name="Id">Entry identifier.</param>
/// <param name="OccurredOn">Date it happened.</param>
/// <param name="Description">What it was.</param>
/// <param name="Kind">
/// <c>income</c>, <c>expense</c>, <c>transfer</c> or <c>opening</c>.
/// </param>
/// <param name="Amount">
/// Signed amount: negative for money leaving, positive for money coming in.
/// </param>
/// <param name="AccountName">Real account affected.</param>
/// <param name="CategoryName">Category, when the entry has one.</param>
/// <param name="CategoryColorIndex">That category's tone, when there is one.</param>
/// <param name="IsRecurring">Whether the entry came from a recurring rule.</param>
public sealed record LedgerEntrySummary(
    Guid Id,
    DateOnly OccurredOn,
    string Description,
    string Kind,
    decimal Amount,
    string AccountName,
    string? CategoryName,
    int? CategoryColorIndex,
    bool IsRecurring);
