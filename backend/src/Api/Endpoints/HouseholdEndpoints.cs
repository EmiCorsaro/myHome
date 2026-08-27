using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Contracts.Households;

namespace MyHome.Api.Endpoints;

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/household").WithTags("Household");

        group.MapGet("/", GetCurrentHouseholdAsync)
            .WithName("GetCurrentHousehold")
            .WithSummary("Returns the current request's household together with its members.")
            .Produces<HouseholdSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCurrentHouseholdAsync(
        IHouseholdDirectory directory,
        CancellationToken cancellationToken)
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
