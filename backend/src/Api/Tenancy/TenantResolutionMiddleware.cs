using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IHouseholdResolver resolver,
        AmbientTenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(tenant);

        var resolved = await resolver.ResolveAsync(context.RequestAborted).ConfigureAwait(false);

        if (resolved is null)
        {
            await Results.Problem(
                    title: "Not authenticated",
                    detail: "The request cannot be attributed to any household.",
                    statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context)
                .ConfigureAwait(false);

            return;
        }

        tenant.Resolve(resolved.Value.HouseholdId, resolved.Value.MemberId);

        await next(context).ConfigureAwait(false);
    }
}
