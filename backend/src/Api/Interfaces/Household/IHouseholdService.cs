namespace MyHome.Api.Interfaces.Household;

public interface IHouseholdService
{
    Task<IResult> GetCurrentHouseholdAsync(CancellationToken cancellationToken);
}
