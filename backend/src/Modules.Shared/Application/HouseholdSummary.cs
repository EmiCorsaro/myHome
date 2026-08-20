namespace MyHome.Modules.Shared.Application;

/// <summary>
/// Read-only view of a household and its members, shaped for the API.
/// </summary>
/// <param name="Id">Household identifier.</param>
/// <param name="Name">Household name.</param>
/// <param name="BaseCurrency">Base currency, as a three-letter ISO 4217 code.</param>
/// <param name="TimeZoneId">The household's IANA time zone.</param>
/// <param name="Members">Members, in display order.</param>
/// <remarks>
/// It is deliberately a separate type from the entity. The entity can grow internal fields
/// without changing the API contract, and the contract can change shape without touching the
/// domain. Conflating the two is what makes a column rename break a mobile client.
/// </remarks>
public sealed record HouseholdSummary(
    Guid Id,
    string Name,
    string BaseCurrency,
    string TimeZoneId,
    IReadOnlyList<HouseholdMemberSummary> Members);

/// <summary>
/// Read-only view of a household member.
/// </summary>
/// <param name="Id">Member identifier.</param>
/// <param name="DisplayName">Visible name.</param>
/// <param name="Role">Role within the household.</param>
/// <param name="HasAccount">Whether the member has a sign-in account.</param>
public sealed record HouseholdMemberSummary(
    Guid Id,
    string DisplayName,
    string Role,
    bool HasAccount);
