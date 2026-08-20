using MyHome.Modules.Shared.Tenancy;

namespace MyHome.Api.Tenancy;

/// <summary>
/// Resolves each request's household before it reaches any endpoint.
/// </summary>
/// <remarks>
/// Sits at the top of the pipeline so that from here on any service asking for an
/// <see cref="ITenantContext"/> gets an already-resolved household.
/// <para>
/// No household means a 401 right here. An endpoint that can run without a resolved tenant is the
/// shortest path to a cross-household leak.
/// </para>
/// </remarks>
/// <param name="next">Next element in the pipeline.</param>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Resolves the tenant and hands over, or replies 401 if it could not be resolved.
    /// </summary>
    /// <param name="context">HTTP request context.</param>
    /// <param name="resolver">Household resolver for the current environment.</param>
    /// <param name="tenant">This request's tenant context, not yet resolved.</param>
    /// <returns>A task that completes once the request has been handled.</returns>
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
