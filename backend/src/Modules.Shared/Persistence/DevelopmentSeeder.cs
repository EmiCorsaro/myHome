using MyHome.Modules.Shared.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Shared.Persistence;

/// <summary>
/// Creates the schema and a working household when the database is empty.
/// </summary>
/// <remarks>
/// Development only. Applies no migrations — fine while the schema changes daily, replaced by
/// <c>MigrateAsync</c> once there is data worth keeping.
/// </remarks>
public static class DevelopmentSeeder
{
    /// <summary>Tables this schema expects to find.</summary>
    private static readonly string[] ExpectedTables = ["households", "household_members"];

    /// <summary>
    /// Ensures the schema and at least one household exist, opening its own scope.
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
    /// Ensures the schema and at least one household exist.
    /// </summary>
    /// <param name="db">Shared data context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing household, or the newly created one.</returns>
    public static async Task<Household> EnsureSeededAsync(
        SharedDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await EnsureTablesAsync(db, cancellationToken).ConfigureAwait(false);

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

    /// <summary>Creates the schema if it is missing or out of date.</summary>
    /// <param name="db">Shared data context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the tables are in place.</returns>
    /// <remarks>
    /// EnsureCreated is no use here: it asks whether the database has any tables at all, and a
    /// database that was never truly empty — a Supabase project ships with its own <c>auth</c>
    /// and <c>storage</c> tables, for instance — makes it decide there is nothing to do.
    /// <para>
    /// A missing table means the model has moved on, so the schema is dropped and rebuilt.
    /// Development data is disposable; this stops being acceptable the day migrations arrive.
    /// </para>
    /// </remarks>
    private static async Task EnsureTablesAsync(
        SharedDbContext db,
        CancellationToken cancellationToken)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await creator.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        var present = await db.Database
            .SqlQuery<string>(
                $"""
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_schema = {SharedDbContext.Schema}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ExpectedTables.All(present.Contains))
        {
            return;
        }

        if (present.Count > 0)
        {
            await db.Database
                .ExecuteSqlRawAsync(
                    $"DROP SCHEMA IF EXISTS {SharedDbContext.Schema} CASCADE",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }
}
