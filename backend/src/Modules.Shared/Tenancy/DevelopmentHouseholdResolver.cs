using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Shared.Persistence;

namespace MyHome.Modules.Shared.Tenancy;

/// <summary>
/// Holds the household resolved for this run of the process.
/// </summary>
/// <remarks>
/// Registered as a singleton. In development the household is fixed for the lifetime of the
/// process, so looking it up on every request is a database round trip that can only ever return
/// the same answer.
/// </remarks>
internal sealed class DevelopmentHouseholdCache
{
    private (Guid HouseholdId, Guid? MemberId)? _resolved;

    /// <summary>
    /// Returns the cached household, resolving it the first time it is asked for.
    /// </summary>
    /// <param name="resolve">How to look it up when the cache is empty.</param>
    /// <returns>The household, or <see langword="null"/> if the database has none yet.</returns>
    /// <remarks>
    /// No lock. Several requests arriving together on the first page load may each run the query
    /// once; it is a read, and paying for a semaphore to avoid a handful of duplicate reads on
    /// startup is not a good trade.
    /// <para>
    /// Nothing here takes a cancellation token, on purpose. This runs before the endpoint and
    /// cancelling it would only mean doing it again on the next request.
    /// </para>
    /// </remarks>
    public async Task<(Guid HouseholdId, Guid? MemberId)?> GetOrResolveAsync(
        Func<Task<(Guid HouseholdId, Guid? MemberId)?>> resolve)
    {
        if (_resolved is { } cached)
        {
            return cached;
        }

        var value = await resolve().ConfigureAwait(false);

        // An empty database is not cached: the seeder may still be running.
        if (value is not null)
        {
            _resolved = value;
        }

        return value;
    }
}

/// <summary>
/// Household resolver for development: returns the first household in the database.
/// </summary>
/// <remarks>
/// <b>Must not be registered outside development.</b> It checks no credentials: any request is
/// attributed to the working household. It exists so phase 1 can proceed without waiting on the
/// identity provider decision, and its registration is gated on the environment so it cannot
/// slip into production by accident.
/// </remarks>
/// <param name="db">Shared data context.</param>
/// <param name="cache">Household resolved for this run.</param>
internal sealed class DevelopmentHouseholdResolver(
    SharedDbContext db,
    DevelopmentHouseholdCache cache) : IHouseholdResolver
{
    /// <inheritdoc />
    public Task<(Guid HouseholdId, Guid? MemberId)?> ResolveAsync(
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
                        .Select(m => (Guid?)m.Id)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return household is null ? null : (household.Id, household.OwnerId);
        });
}
