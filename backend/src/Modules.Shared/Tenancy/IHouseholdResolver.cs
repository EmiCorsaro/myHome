namespace MyHome.Modules.Shared.Tenancy;

/// <summary>
/// Decides which household a request belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface is the authentication seam.</b> Today it has a development implementation
/// that returns the only household there is. When an identity provider is chosen — Entra
/// External ID or similar — a second implementation will read the request's identity and look up
/// its member.
/// </para>
/// <para>
/// Nothing else in the system changes that day: not the domain, not the services, not the
/// endpoints. Isolating the decision here is what makes choosing a provider non-urgent.
/// </para>
/// </remarks>
public interface IHouseholdResolver
{
    /// <summary>
    /// Resolves the household and member for the current request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The household and, if known, the member. <c>null</c> when the request cannot be
    /// attributed to any household, which becomes a 401.
    /// </returns>
    Task<(Guid HouseholdId, Guid? MemberId)?> ResolveAsync(
        CancellationToken cancellationToken = default);
}
