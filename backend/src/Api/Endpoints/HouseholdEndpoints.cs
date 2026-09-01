using MyHome.Api.Interfaces.Household;
using MyHome.Modules.Shared.Contracts.Households;

namespace MyHome.Api.Endpoints;

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/household").WithTags("Household");

        group.MapGet("/", (IHouseholdService households, CancellationToken cancellationToken) => households.GetCurrentHouseholdAsync(cancellationToken))
            .WithName("GetCurrentHousehold")
            .WithSummary("Returns the current request's household together with its members.")
            .Produces<HouseholdSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
