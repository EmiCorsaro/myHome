using MyHome.Modules.Shared.Persistence;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Shared.Application;

/// <summary>
/// Implementation of <see cref="IHouseholdDirectory"/> over <see cref="SharedDbContext"/>.
/// </summary>
/// <param name="db">Shared data context.</param>
/// <param name="tenant">Tenant context for the current request.</param>
public sealed class HouseholdDirectory(SharedDbContext db, ITenantContext tenant)
    : IHouseholdDirectory
{
    /// <inheritdoc />
    public async Task<HouseholdSummary?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var householdId = tenant.RequireHouseholdId();

        // AsNoTracking because this is a read: without change tracking EF skips building the
        // identity graph, and the query is noticeably cheaper.
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
                m.Id,
                m.DisplayName,
                m.Role.ToString(),
                m.UserId.HasValue))
            .ToList();

        return new HouseholdSummary(
            household.Id,
            household.Name,
            household.BaseCurrency.Value,
            household.TimeZoneId,
            members);
    }
}
