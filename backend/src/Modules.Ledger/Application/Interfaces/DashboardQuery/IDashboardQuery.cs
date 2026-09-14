using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Dashboard;

namespace MyHome.Modules.Ledger.Application;

public interface IDashboardQuery
{
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The dashboard of the month named by a year and a month, or of the household's current month
    /// when neither is given (story 062).
    /// </summary>
    /// <exception cref="Shared.Contracts.ValidationFailedException">
    /// The year or the month is missing its other half, is not a whole number or is out of range;
    /// each error is keyed by the query parameter that caused it (<c>year</c>, <c>month</c>).
    /// </exception>
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DashboardMonthRequest request,
        CancellationToken cancellationToken = default);
}
