using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Contracts.Dashboard;

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

public sealed record CategoryTotal(
    Guid CategoryId,
    string Name,
    int ColorIndex,
    decimal Total,
    decimal Share);

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
