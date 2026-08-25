namespace MyHome.Modules.Shared.Tenancy;

public interface IHouseholdResolver
{
    Task<(int HouseholdId, int? MemberId)?> ResolveAsync(
        CancellationToken cancellationToken = default);
}
