using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Dashboard;

namespace MyHome.Modules.Ledger.Application;

public interface IDashboardQuery
{
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default);
}
