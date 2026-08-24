using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyHome.Modules.Ledger.Persistence;

/// <summary>
/// Brings the Ledger module's database schema up to date.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="LedgerSeeder"/> on purpose: the schema is applied in every
/// environment, the starting accounts and categories only in development.
/// </para>
/// <para>
/// The module owns its own migration history — <c>ledger.__ef_migrations_history</c> — so it
/// versions independently of the shared kernel and of every other module. That is what keeps
/// extracting this module to its own service a deployment change rather than a data migration.
/// </para>
/// </remarks>
public static class LedgerSchema
{
    /// <summary>
    /// Applies any pending migrations, opening its own scope.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the schema matches the model.</returns>
    /// <remarks>
    /// See <see cref="Modules.Shared.Persistence.SharedSchema.MigrateAsync"/> for when applying
    /// migrations from the application stops being the right answer.
    /// </remarks>
    /// <example>
    /// <code>
    /// await LedgerSchema.MigrateAsync(app.Services);
    /// </code>
    /// </example>
    public static async Task MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
