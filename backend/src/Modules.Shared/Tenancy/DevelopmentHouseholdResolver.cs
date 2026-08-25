using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Shared.Persistence;

namespace MyHome.Modules.Shared.Tenancy;

internal sealed class DevelopmentHouseholdCache
{
    private (int HouseholdId, int? MemberId)? _resolved;

    public async Task<(int HouseholdId, int? MemberId)?> GetOrResolveAsync(
        Func<Task<(int HouseholdId, int? MemberId)?>> resolve)
    {
        if (_resolved is { } cached)
        {
            return cached;
        }

        var value = await resolve().ConfigureAwait(false);

        if (value is not null)
        {
            _resolved = value;
        }

        return value;
    }
}

internal sealed class DevelopmentHouseholdResolver(
    SharedDbContext db,
    DevelopmentHouseholdCache cache) : IHouseholdResolver
{
    public Task<(int HouseholdId, int? MemberId)?> ResolveAsync(
        CancellationToken cancellationToken = default) =>
        cache.GetOrResolveAsync(async () =>
        {
            var household = await db.Households
                .AsNoTracking()
                .OrderBy(h => h.CreatedAt)
                .Select(h => new
                {
                    h.Id,
                    OwnerId = h.Members
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => (int?)m.Id)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return household is null ? null : (household.Id, household.OwnerId);
        });
}
