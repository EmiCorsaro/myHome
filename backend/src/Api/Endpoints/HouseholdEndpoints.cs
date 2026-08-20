using MyHome.Modules.Shared.Application;

namespace MyHome.Api.Endpoints;

/// <summary>
/// Household endpoints.
/// </summary>
/// <remarks>
/// No query, no rule, no calculation: bind, delegate, map to a status. An architecture test
/// asserts this project cannot reach the database even if someone tries.
/// </remarks>
public static class HouseholdEndpoints
{
    /// <summary>
    /// Registers the household endpoints.
    /// </summary>
    /// <param name="app">The application's route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapHouseholdEndpoints();
    /// </code>
    /// </example>
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
