namespace MyHome.Modules.Shared.Tenancy;

public sealed class AmbientTenantContext : ITenantContext
{
    public int? HouseholdId { get; private set; }

    public int? MemberId { get; private set; }

    public void Resolve(int householdId, int? memberId = null)
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
