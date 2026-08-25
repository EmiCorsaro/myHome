namespace MyHome.Modules.Shared.Tenancy;

public interface ITenantContext
{
    int? HouseholdId { get; }

    int? MemberId { get; }

    bool IsResolved => HouseholdId.HasValue;

    int RequireHouseholdId() =>
        HouseholdId ?? throw new InvalidOperationException(
            "No household resolved for this request. Every domain operation needs one.");
}
