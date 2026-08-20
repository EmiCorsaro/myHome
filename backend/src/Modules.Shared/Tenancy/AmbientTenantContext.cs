namespace MyHome.Modules.Shared.Tenancy;

/// <summary>
/// Request-scoped implementation of <see cref="ITenantContext"/>, filled in once the caller has
/// been resolved.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <c>Scoped</c>: one instance per HTTP request. Middleware resolves it at the top
/// of the pipeline, and from then on the whole service graph sees the same household.
/// </para>
/// <para>
/// <b>Authentication seam.</b> Until an identity provider is chosen, middleware fills this in
/// with the development household. When the real provider arrives, only the caller of
/// <see cref="Resolve"/> changes: neither the domain nor the repositories notice.
/// </para>
/// </remarks>
public sealed class AmbientTenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid? HouseholdId { get; private set; }

    /// <inheritdoc />
    public Guid? MemberId { get; private set; }

    /// <summary>
    /// Sets the household and member for the current request.
    /// </summary>
    /// <param name="householdId">The household the request belongs to.</param>
    /// <param name="memberId">The member making it, if known.</param>
    /// <exception cref="InvalidOperationException">
    /// If it was already resolved. The tenant is set once per request: allowing it to change
    /// midway would be a direct route to cross-household leaks.
    /// </exception>
    public void Resolve(Guid householdId, Guid? memberId = null)
    {
        if (HouseholdId.HasValue)
        {
            throw new InvalidOperationException(
                "The tenant was already resolved for this request and cannot be changed.");
        }

        HouseholdId = householdId;
        MemberId = memberId;
    }
}
