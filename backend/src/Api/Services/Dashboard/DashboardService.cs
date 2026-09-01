using MyHome.Api.Interfaces.Dashboard;
using MyHome.Modules.Ledger.Application;

namespace MyHome.Api.Services.Dashboard;

internal sealed class DashboardService(IDashboardQuery dashboard) : IDashboardService
{
    public async Task<IResult> GetDashboardAsync(DateOnly? month, CancellationToken cancellationToken)
    {
        var summary = await dashboard.GetMonthlySummaryAsync(month, cancellationToken).ConfigureAwait(false);

        return Results.Ok(summary);
    }
}
