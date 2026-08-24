using MyHome.Modules.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared.Persistence;

/// <summary>
/// Creates a working household when there is none.
/// </summary>
/// <remarks>
/// Development only, and data only: the schema is <see cref="SharedSchema"/>'s job and has already
/// been applied by the time this runs.
/// </remarks>
public static class DevelopmentSeeder
{
    /// <summary>
    /// Ensures at least one household exists, opening its own scope.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The identifier of the working household, so the composition root can hand it to each
    /// module's own seeder without querying for it.
    /// </returns>
    /// <remarks>
    /// Exists so the composition root never names the data context: the Api project picks up no
    /// persistence dependency and the architecture test asserting that keeps passing. Returns a
    /// <see cref="Guid"/> rather than the entity for the same reason.
    /// </remarks>
    public static async Task<Guid> EnsureSeededAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();

        var household = await EnsureSeededAsync(db, cancellationToken).ConfigureAwait(false);

        return household.Id;
    }

    /// <summary>
    /// Ensures at least one household exists.
    /// </summary>
    /// <param name="db">Shared data context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing household, or the newly created one.</returns>
    public static async Task<Household> EnsureSeededAsync(
        SharedDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existing = await db.Households
            .Include(h => h.Members)
            .OrderBy(h => h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var household = Household.Create(
            name: "Development household",
            ownerDisplayName: "Member A",
            baseCurrency: CurrencyCode.Euro,
            timeZoneId: "Europe/Madrid");

        household.AddMember("Member B", MemberRole.Member);

        db.Households.Add(household);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return household;
    }
}
