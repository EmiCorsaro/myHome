using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Contracts.Dashboard;

namespace MyHome.Modules.Ledger.Application;

public interface IDashboardQuery
{
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntrySummary>> ListRealMovementsAsync(
        CategoryNature nature,
        DateOnly? from = null,
        DateOnly? upperBound = null,
        CancellationToken cancellationToken = default);
}
