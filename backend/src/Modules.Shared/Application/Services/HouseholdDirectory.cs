using MyHome.Modules.Shared.Contracts.Households;
using MyHome.Modules.Shared.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Shared.Application;

internal sealed class HouseholdDirectory(SharedDbContext db, ITenantContext tenant)
    : IHouseholdDirectory
{
    public async Task<HouseholdSummary?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var household = await db.Households
            .AsNoTracking()
            .Include(h => h.Members)
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken)
            .ConfigureAwait(false);

        if (household is null)
        {
            return null;
        }

        var members = household.Members
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new HouseholdMemberSummary(
                m.PublicId,
                m.DisplayName,
                m.Role.ToString(),
                m.UserId.HasValue))
            .ToList();

        return new HouseholdSummary(
            household.PublicId,
            household.Name,
            household.BaseCurrency.Value,
            household.TimeZoneId,
            members);
    }

    public async Task<int?> ResolveMemberAsync(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        var id = await db.Members
            .AsNoTracking()
            .Where(m => m.PublicId == publicId && m.HouseholdId == householdId)
            .Select(m => (int?)m.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return id;
    }
}
