using MyHome.Api.Interfaces.Household;
using MyHome.Modules.Shared.Application;

namespace MyHome.Api.Services.Household;

internal sealed class HouseholdService(IHouseholdDirectory directory) : IHouseholdService
{
    public async Task<IResult> GetCurrentHouseholdAsync(CancellationToken cancellationToken)
    {
        var household = await directory.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        return household is null
            ? Results.Problem(
                title: "Household not found",
                detail: "The household resolved for this request no longer exists.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(household);
    }
}
