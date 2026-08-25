using MyHome.Modules.Shared.Contracts.Households;

namespace MyHome.Modules.Shared.Application;

public interface IHouseholdDirectory
{
    Task<HouseholdSummary?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<int?> ResolveMemberAsync(Guid publicId, CancellationToken cancellationToken = default);
}
