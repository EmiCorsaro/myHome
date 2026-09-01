namespace MyHome.Api.Interfaces.Dashboard;

public interface IDashboardService
{
    Task<IResult> GetDashboardAsync(DateOnly? month, CancellationToken cancellationToken);
}
