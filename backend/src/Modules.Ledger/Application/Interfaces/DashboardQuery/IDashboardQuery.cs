using MyHome.Modules.Ledger.Contracts;
using MyHome.Modules.Ledger.Contracts.Dashboard;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Builds the landing screen's figures.
/// </summary>
/// <remarks>
/// Takes no household identifier: it comes from the request's tenant context inside the
/// implementation. See <see cref="LedgerModule"/>.
/// </remarks>
public interface IDashboardQuery
{
    /// <summary>
    /// Returns the summary for the month containing <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">
    /// Any day in the month of interest. Defaults to today in the household's time zone.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The summary. Never <see langword="null"/>: an empty household returns zeros.</returns>
    /// <remarks>
    /// Zeros rather than nothing, so the screen has one shape with or without data and the empty
    /// case is not a second layout to maintain.
    /// </remarks>
    Task<DashboardSummary> GetMonthlySummaryAsync(
        DateOnly? reference = null,
        CancellationToken cancellationToken = default);
}
