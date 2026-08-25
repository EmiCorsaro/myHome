namespace MyHome.Modules.Shared.Contracts.Households;

public sealed record HouseholdSummary(
    Guid Id,
    string Name,
    string BaseCurrency,
    string TimeZoneId,
    IReadOnlyList<HouseholdMemberSummary> Members);

public sealed record HouseholdMemberSummary(
    Guid Id,
    string DisplayName,
    string Role,
    bool HasAccount);
